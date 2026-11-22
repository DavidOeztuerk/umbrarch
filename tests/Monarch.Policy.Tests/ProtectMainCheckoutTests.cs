using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>Q-0193: Regel protect_main_checkout (ADR-0020 Abschnitt 1). Test-Repos sind Verzeichnisstrukturen unter einem Temp-Ordner.</summary>
public sealed class ProtectMainCheckoutTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "pmc-" + Guid.NewGuid().ToString("N"));
    private readonly string _main;
    private readonly string _wt;
    private readonly string _plain;
    private readonly string _wtOfPlain;

    public ProtectMainCheckoutTests()
    {
        Directory.CreateDirectory(_tmp);
        _main = Path.Combine(_tmp, "main");
        _plain = Path.Combine(_tmp, "plain");
        _wt = Path.Combine(_main, ".claude", "worktrees", "G-1");
        _wtOfPlain = Path.Combine(_plain, ".claude", "worktrees", "G-1");
        Directory.CreateDirectory(Path.Combine(_main, ".monarch"));
        Directory.CreateDirectory(Path.Combine(_main, "src"));
        MakeRepo(_main);
        MakeRepo(_plain);
        MakeWorktree(_main, _wt);
        MakeWorktree(_plain, _wtOfPlain);
        Directory.CreateDirectory(Path.Combine(_wt, "src"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, true); } catch (IOException) { /* best effort */ }
    }

    private static void MakeRepo(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        File.WriteAllText(Path.Combine(root, ".git", "HEAD"), "ref: refs/heads/main\n");
    }

    private static void MakeWorktree(string root, string wt)
    {
        var gitDir = Path.Combine(root, ".git", "worktrees", "G-1");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/monarch/G-1\n");
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..\n");
        Directory.CreateDirectory(wt);
        File.WriteAllText(Path.Combine(wt, ".git"), "gitdir: " + gitDir + "\n");
    }

    private PolicyContext Ctx() =>
        new(_main, new ActiveQuest(new QuestId("Q-1"), Rank.A, new QuestScope(["src/**"], [])), false, new PolicyOptions());

    private PolicyDecision Run(string cmd, string cwd) =>
        ToolCallPolicy.Evaluate(new ToolCall("Bash", Command: cmd, Cwd: cwd), Ctx());

    private void Deny(string cmd, string cwd, bool rule = true)
    {
        var d = Run(cmd, cwd);
        Assert.True(d.Kind == PolicyDecisionKind.Deny, $"expected deny for: {cmd} (cwd {cwd}; was {d.Kind}: {d.Reason})");
        if (rule) Assert.Equal(PolicyRules.ProtectMainCheckout, d.Rule);
        var p = ToolCallPolicy.EvaluateProtection(new ToolCall("Bash", Command: cmd, Cwd: cwd), Ctx());
        Assert.Equal(PolicyDecisionKind.Deny, p.Kind);
        if (rule) Assert.Equal(PolicyRules.ProtectMainCheckout, p.Rule);
    }

    private void Allow(string cmd, string cwd)
    {
        var d = Run(cmd, cwd);
        Assert.True(d.Kind == PolicyDecisionKind.Allow, $"expected allow for: {cmd} (cwd {cwd}; was {d.Kind}: {d.Rule} {d.Reason})");
    }

    [Theory]
    [InlineData("git checkout main")]
    [InlineData("git switch main")]
    [InlineData("git reset --hard")]
    [InlineData("git merge develop")]
    [InlineData("git pull")]
    [InlineData("git commit -m x")]
    [InlineData("git rebase main")]
    [InlineData("git stash")]
    [InlineData("git stash pop")]
    [InlineData("git --no-pager -c user.name=x commit -m x")]
    public void Listed_commands_with_cwd_in_the_main_checkout_are_denied(string cmd)
    {
        Deny(cmd, _main, !cmd.StartsWith("git reset", StringComparison.Ordinal));
        Deny(cmd, Path.Combine(_main, "src"), !cmd.StartsWith("git reset", StringComparison.Ordinal));
    }

    public static TheoryData<string, string> CommandsAndForms()
    {
        string[] commands = ["checkout dev", "switch dev", "reset HEAD~1", "merge dev", "pull", "commit -m x", "rebase dev", "stash", "stash pop", "stash -m list"];
        string[] forms = ["cwd", "cwd-src", "cd", "-C", "--git-dir", "GIT_DIR", "--work-tree", "GIT_WORK_TREE", "--namespace", "env -C", "env --chdir"];
        var data = new TheoryData<string, string>();
        foreach (var c in commands)
            foreach (var f in forms)
                data.Add(c, f);
        return data;
    }

    [Theory]
    [MemberData(nameof(CommandsAndForms))]
    public void Boss_table_every_listed_command_and_target_form_aimed_at_the_main_checkout_is_denied(string cmd, string form)
    {
        var (line, cwd) = form switch
        {
            "cwd" => ($"git {cmd}", _main),
            "cwd-src" => ($"git {cmd}", Path.Combine(_main, "src")),
            "cd" => ($"cd {_main} && git {cmd}", _wt),
            "-C" => ($"git -C {_main} {cmd}", _wt),
            "--git-dir" => ($"git --git-dir={_main}/.git {cmd}", _tmp),
            "GIT_DIR" => ($"GIT_DIR={_main}/.git git {cmd}", _tmp),
            "--work-tree" => ($"git --work-tree={_main}/src {cmd}", _tmp),
            "GIT_WORK_TREE" => ($"GIT_WORK_TREE={_main} git {cmd}", _wt),
            "--namespace" => ($"git --namespace x -C {_main} {cmd}", _wt),
            "env -C" => ($"env -C {_main} git {cmd}", _wt),
            "env --chdir" => ($"env --chdir={_main} git {cmd}", _wt),
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };
        Deny(line, cwd);
    }

    [Theory]
    [MemberData(nameof(CommandsAndForms))]
    public void Boss_table_the_same_forms_aimed_at_a_worktree_are_allowed(string cmd, string form)
    {
        var wtGitDir = Path.Combine(_main, ".git", "worktrees", "G-1");
        var (line, cwd) = form switch
        {
            "cwd" => ($"git {cmd}", _wt),
            "cwd-src" => ($"git {cmd}", Path.Combine(_wt, "src")),
            "cd" => ($"cd {_wt} && git {cmd}", _tmp),
            "-C" => ($"git -C {_wt} {cmd}", _main),
            "--git-dir" => ($"git --git-dir={wtGitDir} {cmd}", _tmp),
            "GIT_DIR" => ($"GIT_DIR={wtGitDir} git {cmd}", _tmp),
            "--work-tree" => ($"git --work-tree={_wt}/src {cmd}", _tmp),
            "GIT_WORK_TREE" => ($"GIT_WORK_TREE={_wt} git {cmd}", _tmp),
            "--namespace" => ($"git --namespace x -C {_wt} {cmd}", _main),
            "env -C" => ($"env -C {_wt} git {cmd}", _main),
            "env --chdir" => ($"env --chdir={_wt} git {cmd}", _main),
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };
        Allow(line, cwd);
    }

    [Theory]
    [InlineData("git --namespace x commit -m x")]
    [InlineData("git --config-env core.editor=EDITOR commit -m x")]
    [InlineData("git --attr-source HEAD commit -m x")]
    [InlineData("git stash -m list")]
    [InlineData("git stash -- show")]
    public void Options_with_a_separate_value_and_stash_push_forms_are_denied(string cmd) => Deny(cmd, _main);

    [Theory]
    [InlineData("env -C {0} git commit -m x")]
    [InlineData("env --chdir={0} git commit -m x")]
    [InlineData("env --chdir {0} git commit -m x")]
    [InlineData("env -u FOO -C {0} git commit -m x")]
    [InlineData("env --unset FOO -C {0} git commit -m x")]
    [InlineData("env -C /tmp -C {0} git commit -m x")]
    [InlineData("env -C / env -C {0} git commit -m x")]
    public void Env_chdir_into_the_main_checkout_is_denied(string template) => Deny(string.Format(template, _main), _wt);

    [Fact]
    public void Env_takes_only_the_last_chdir_of_one_call() => Deny("env -C /tmp -C . git commit -m x", _main);

    [Fact]
    public void Git_shallow_file_with_a_separate_value_does_not_hide_the_subcommand() => Deny("git --shallow-file x commit -m x", _main);

    [Theory]
    [InlineData("env -C{0} git commit -m x")]
    [InlineData("env -vC {0} git commit -m x")]
    [InlineData("env -iC {0} git commit -m x")]
    [InlineData("env -iC{0} git commit -m x")]
    [InlineData("env -i -u FOO -C {0} git commit -m x")]
    public void Env_short_options_in_getopt_form_are_read(string template) => Deny(string.Format(template, _main), _wt);

    [Theory]
    [InlineData("env -C {0} sh -c \"git commit -m x\"")]
    [InlineData("env -C {0} bash -c 'git commit -m x'")]
    [InlineData("env -vC {0} sh -c \"git commit -m x\"")]
    public void Env_chdir_also_applies_to_the_shell_code_of_sh_c(string template) => Deny(string.Format(template, _main), _wt);

    [Fact]
    public void Env_chdir_into_a_worktree_before_sh_c_is_allowed() => Allow($"env -C {_wt} sh -c \"git commit -m x\"", _main);

    [Fact]
    public void Env_chdir_before_a_shell_reading_a_heredoc_applies_to_its_code() =>
        Deny($"env -C {_main} sh <<'EOF'\ngit commit -m x\nEOF", _wt);

    [Fact]
    public void Env_chdir_of_the_shell_behind_a_pipe_applies_to_the_heredoc_code() =>
        Deny($"cat <<'EOF' | env -C {_main} sh\ngit commit -m x\nEOF", _wt);

    [Fact]
    public void Env_chdir_of_the_command_before_the_pipe_does_not_move_the_shell() =>
        Deny($"env -C {_wt} cat <<'EOF' | sh\ngit commit -m x\nEOF", _main);

    [Theory]
    [InlineData("env --ch {0} git commit -m x")]
    [InlineData("env --chd={0} git commit -m x")]
    [InlineData("env --u FOO --c {0} git commit -m x")]
    public void Env_long_options_may_be_abbreviated(string template) => Deny(string.Format(template, _main), _wt);

    [Fact]
    public void Basis_git_checkout_main_in_the_main_checkout_is_denied() => Deny("git checkout main", _main);

    [Fact]
    public void Basis_git_commit_in_the_main_checkout_is_denied() => Deny("git commit -m x", _main);

    [Theory]
    [InlineData("git stash list")]
    [InlineData("git stash show")]
    [InlineData("git worktree add .claude/worktrees/G-2 -b monarch/G-2 origin/develop")]
    [InlineData("git worktree remove .claude/worktrees/G-2")]
    [InlineData("git worktree list")]
    [InlineData("git worktree prune")]
    [InlineData("git fetch origin")]
    [InlineData("git status")]
    [InlineData("git log -3")]
    [InlineData("git diff")]
    public void Exceptions_and_read_only_commands_in_the_main_checkout_are_allowed(string cmd) => Allow(cmd, _main);

    [Fact]
    public void Cd_from_a_worktree_into_the_main_checkout_is_denied()
    {
        Deny($"cd {_main} && git commit -m x", _wt);
        Deny($"pushd {_main} && git commit -m x", _wt);
    }

    [Fact]
    public void Git_dash_C_from_a_worktree_into_the_main_checkout_is_denied()
    {
        Deny($"git -C {_main} commit -m x", _wt);
        Deny($"git -C {_main}/src checkout main", _wt);
        Deny($"git -C {_main} -C src checkout main", _wt);
    }

    [Fact]
    public void Git_dash_C_into_a_worktree_with_cwd_in_the_main_checkout_is_allowed()
    {
        Allow($"git -C {_wt} commit -m x", _main);
        Allow($"git -C {_wt} checkout main", _main);
    }

    [Fact]
    public void Git_dir_pointing_at_the_main_checkout_is_denied_from_a_worktree_or_a_folder_without_repo()
    {
        Deny($"git --git-dir={_main}/.git checkout dev", _wt);
        Deny($"git --git-dir {_main}/.git checkout dev", _tmp);
        Deny($"GIT_DIR={_main}/.git git checkout dev", _tmp);
        Deny($"export GIT_DIR={_main}/.git; git commit -m x", _tmp);
    }

    [Fact]
    public void Git_dir_in_other_case_is_denied_where_the_file_system_ignores_case()
    {
        var upper = Path.Combine(_main, ".GIT");
        // Unterscheidet das Dateisystem die Schreibweise, findet auch Git dieses Verzeichnis nicht.
        if (!Directory.Exists(upper)) return;
        Deny($"git --git-dir={upper} checkout dev", _tmp);
    }

    [Fact]
    public void Work_tree_pointing_at_the_main_checkout_is_denied_even_for_a_subfolder()
    {
        Deny($"git --work-tree={_main} checkout dev", _tmp);
        Deny($"git --work-tree {_main}/src checkout dev", _tmp);
        Deny($"GIT_WORK_TREE={_main} git checkout dev", _tmp);
        Deny($"GIT_WORK_TREE={_main}/src git checkout dev", _wt);
        Deny($"git --work-tree={_main}/src checkout -f -- .", _wt, rule: false);
        Deny("git --work-tree=../../../src checkout dev", _wt);
    }

    [Fact]
    public void Work_tree_outside_any_repo_still_denies_with_cwd_in_the_main_checkout()
    {
        Deny("git --work-tree=/tmp/x checkout main", _main);
        Deny("GIT_DIR=/tmp/y/.git git status; git checkout main", _main);
    }

    [Fact]
    public void Git_dir_of_a_worktree_with_cwd_in_the_main_checkout_is_denied_strictly()
    {
        Deny($"git --git-dir={_main}/.git/worktrees/G-1 commit -m x", _main);
        Deny($"GIT_DIR={_main}/.git/worktrees/G-1 git commit -m x", _main);
    }

    [Fact]
    public void Worktrees_of_the_same_repo_are_untouched()
    {
        Allow("git commit -m x", _wt);
        Allow("git checkout main", _wt);
        Allow("git stash", _wt);
        Allow($"cd {_wt} && git commit -m x", _tmp);
        Allow($"git --git-dir={_main}/.git/worktrees/G-1 --work-tree={_wt} commit -m x", _tmp);
    }

    [Fact]
    public void Repos_without_a_monarch_folder_are_untouched()
    {
        Allow("git commit -m x", _plain);
        Allow("git checkout main", _plain);
        Allow($"git -C {_plain} commit -m x", _tmp);
        Allow($"git --work-tree={_plain} checkout dev", _tmp);
        Allow($"GIT_DIR={_plain}/.git git commit -m x", _tmp);
        Allow("git commit -m x", _wtOfPlain);
    }

    [Fact]
    public void Non_git_commands_and_other_words_do_not_trigger_the_rule()
    {
        Allow("echo checkout main", _main);
        Allow("ls", _main);
    }

    [Fact]
    public void The_deny_text_names_the_way_out()
    {
        var d = Run("git commit -m x", _main);
        Assert.Contains("Gate-Worktree", d.Reason);
        Assert.Contains("git worktree add .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop", d.Reason);
        Assert.Contains("EnterWorktree", d.Reason);
    }
}
