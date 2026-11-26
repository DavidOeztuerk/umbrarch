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

    [Theory]
    [InlineData("env -C -d sh -c \"git commit -m x\"")]
    [InlineData("env -C-d sh -c \"git commit -m x\"")]
    [InlineData("env -iC -d sh -c \"git commit -m x\"")]
    [InlineData("env --chdir=-d sh -c \"git commit -m x\"")]
    [InlineData("env -C -d sh <<'EOF'\ngit commit -m x\nEOF")]
    [InlineData("cat <<'EOF' | env -C -d sh\ngit commit -m x\nEOF")]
    public void Env_chdir_value_starting_with_a_dash_is_taken_literally(string cmd) => Deny(cmd, _main);

    [Fact]
    public void Env_chdir_dash_is_a_directory_named_dash_not_oldpwd() =>
        Deny($"cd {_wt} && cd {_main} && env -C - sh -c \"git commit -m x\"", _main);

    [Fact]
    public void Env_chdir_value_starting_with_a_dash_inside_a_worktree_is_allowed() =>
        Allow("env -C -d sh -c \"git commit -m x\"", _wt);

    [Theory]
    [InlineData("cd -d; git commit -m x")]
    [InlineData("cd -- -d; git commit -m x")]
    [InlineData("cd -L -- -d; git commit -m x")]
    [InlineData("cd --bogus && git checkout dev")]
    public void Cd_options_are_read_like_bash(string cmd) => Deny(cmd, _main);

    [Fact]
    public void Cd_with_a_known_option_into_a_worktree_is_allowed() =>
        Allow($"cd -L {_wt}; git commit -m x", _main);

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
        Assert.Contains("git -C <Haupt-Checkout> worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop", d.Reason);
        Assert.Contains("im eigenen Terminal", d.Reason);
        Assert.Contains("Session dort", d.Reason);
        Assert.DoesNotContain("EnterWorktree", d.Reason);
    }

    [Theory]
    [InlineData("pushd -n /tmp; git commit -m x")]
    [InlineData("pushd -n /tmp && git commit -m x")]
    [InlineData("pushd -q /tmp; git commit -m x")]
    [InlineData("cd -e /tmp; git commit -m x")]
    [InlineData("cd -@ /tmp; git commit -m x")]
    [InlineData("cd -Pe /tmp; git commit -m x")]
    public void Dir_options_read_differently_by_bash_and_zsh_keep_the_main_checkout(string cmd) => Deny(cmd, _main);

    [Fact]
    public void Pushd_without_options_into_a_worktree_is_allowed() => Allow($"pushd {_wt}; git commit -m x", _main);

    private const string AmbiguousReason = "Arbeitsverzeichnis nicht eindeutig (cd/pushd-Variante); nutze git -C <Gate-Worktree>.";

    private void DenyAmbiguous(string cmd, string cwd)
    {
        Deny(cmd, cwd);
        Assert.Equal(AmbiguousReason, Run(cmd, cwd).Reason);
    }

    [Theory]
    [InlineData("cd -q {0}; git commit -m x")]
    [InlineData("cd -s {0} && git commit -m x")]
    [InlineData("pushd -q {0}; git commit -m x")]
    public void Cd_with_an_option_other_than_L_and_P_keeps_old_and_new_directory(string template) => Deny(string.Format(template, _main), _wt);

    [Theory]
    [InlineData("cd /.claude/worktrees/G-1 /; git commit -m x")]
    [InlineData("pushd /.claude/worktrees/G-1 /; git commit -m x")]
    public void Cd_and_pushd_with_two_operands_are_not_unique(string cmd) => DenyAmbiguous(cmd, _wt);

    [Fact]
    public void Cd_to_a_missing_directory_keeps_the_old_one() => Deny($"cd {Path.Combine(_tmp, "fehlt")}; git commit -m x", _main);

    [Theory]
    [InlineData("pushd /tmp; pushd +1; git commit -m x")]
    [InlineData("pushd /tmp; pushd -1; git commit -m x")]
    [InlineData("pushd /tmp; cd +1; git commit -m x")]
    [InlineData("pushd /tmp; popd +1; git commit -m x")]
    public void Stack_selection_is_not_unique(string cmd) => DenyAmbiguous(cmd, _main);

    [Fact]
    public void Ambiguity_does_not_hit_read_only_git() => Allow("pushd /tmp; popd +1; git stash list", _wt);

    [Fact]
    public void Mkdir_then_cd_into_the_new_directory_in_a_worktree_is_allowed() =>
        Allow("mkdir X && cd X && git commit -m x", _wt);

    [Fact]
    public void Ambiguity_inside_a_subshell_still_counts_afterwards() =>
        Deny($"(pushd /tmp; pushd +1); git -C {_wt} commit -m x", _wt);

    // ---- ADR-0020 §1, Schicht 3: feste Sperrliste im Bash-Text ----

    [Theory]
    [InlineData("git -c core.hooksPath=/dev/null commit -m x")]
    [InlineData("git -c core.hookspath=x commit -m x")]
    [InlineData("GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=core.hooksPath GIT_CONFIG_VALUE_0=/dev/null git commit -m x")]
    [InlineData("GIT_CONFIG_PARAMETERS=\"'core.hooksPath=/x'\" git branch -D develop")]
    [InlineData("env -u CLAUDECODE git commit -m x")]
    [InlineData("CLAUDECODE= git branch -f develop HEAD")]
    [InlineData("unset claudecode; git commit -m x")]
    [InlineData("echo $ClaudeCode")]
    public void Banned_words_in_the_bash_text_are_denied_everywhere(string cmd)
    {
        foreach (var cwd in new[] { _wt, _main, _tmp })
        {
            var d = Run(cmd, cwd);
            Assert.True(d.Kind == PolicyDecisionKind.Deny && d.Rule == PolicyRules.ProtectMainCheckout, $"{cmd} (cwd {cwd}): {d.Kind} {d.Rule}");
            Assert.Contains("Grep-Werkzeug", d.Reason, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git commit -m x")]
    public void Ordinary_commands_in_a_worktree_stay_allowed(string cmd) => Allow(cmd, _wt);

    // ---- Datei-Tools gegen den Haupt-Checkout ----

    private PolicyDecision RunFile(string tool, string path, string cwd) =>
        ToolCallPolicy.Evaluate(new ToolCall(tool, FilePath: path, Cwd: cwd), Ctx());

    private void DenyFile(string tool, string path, string cwd)
    {
        var d = RunFile(tool, path, cwd);
        Assert.True(d.Kind == PolicyDecisionKind.Deny && d.Rule == PolicyRules.ProtectMainCheckout, $"{tool} {path}: {d.Kind} {d.Rule} {d.Reason}");
    }

    private static void Link(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (Directory.Exists(target)) Directory.CreateSymbolicLink(link, target);
        else File.CreateSymbolicLink(link, target);
    }

    [Theory]
    [InlineData("Write")]
    [InlineData("Edit")]
    [InlineData("MultiEdit")]
    [InlineData("NotebookEdit")]
    public void File_tools_aimed_at_the_main_checkout_are_denied(string tool)
    {
        DenyFile(tool, Path.Combine(_main, "src", "a.cs"), _wt);
        DenyFile(tool, Path.Combine(_main, "neu", "tief", "a.cs"), _wt);
    }

    [Fact]
    public void Write_inside_a_worktree_is_allowed() =>
        Assert.NotEqual(PolicyRules.ProtectMainCheckout, RunFile("Write", Path.Combine(_wt, "src", "a.cs"), _wt).Rule);

    [Fact]
    public void Write_through_a_symlinked_directory_into_the_main_checkout_is_denied()
    {
        Link(Path.Combine(_wt, "lnk"), Path.Combine(_main, "src"));
        DenyFile("Write", Path.Combine(_wt, "lnk", "a.cs"), _wt);
    }

    [Fact]
    public void Edit_of_a_symlinked_file_pointing_into_the_main_checkout_is_denied()
    {
        File.WriteAllText(Path.Combine(_main, "x.cs"), "x");
        Link(Path.Combine(_wt, "datei.cs"), Path.Combine(_main, "x.cs"));
        DenyFile("Edit", Path.Combine(_wt, "datei.cs"), _wt);
    }

    /// <summary>Q-0208: Eine Datei commondir, die auf das eigene .git zeigt, macht aus dem Haupt-Checkout keine Worktree (realpath-Vergleich wie im git-Hook).</summary>
    [Theory]
    [InlineData(".")]
    [InlineData("./")]
    [InlineData("../.git")]
    [InlineData("../.GIT")]
    public void Commondir_pointing_at_its_own_git_dir_keeps_the_main_checkout(string commondir)
    {
        File.WriteAllText(Path.Combine(_main, ".git", "commondir"), commondir + "\n");
        Deny("git checkout main", _main);
        DenyFile("Write", Path.Combine(_main, "src", "a.cs"), _wt);
    }

    [Fact]
    public void Commondir_given_as_absolute_path_through_a_symlink_keeps_the_main_checkout()
    {
        Link(Path.Combine(_tmp, "lnk-main"), _main);
        File.WriteAllText(Path.Combine(_main, ".git", "commondir"), Path.Combine(_tmp, "lnk-main", ".git") + "\n");
        Deny("git commit -m x", _main);
        DenyFile("Edit", Path.Combine(_main, "src", "a.cs"), _wt);
    }

    /// <summary>Unklar heißt Haupt-Checkout: leer, unlesbar (ein Ordner) oder ein Ziel, das es nicht gibt.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("dir")]
    [InlineData("../gibt-es-nicht")]
    public void Unclear_commondir_counts_as_main_checkout(string commondir)
    {
        var path = Path.Combine(_main, ".git", "commondir");
        if (commondir == "dir") Directory.CreateDirectory(path);
        else File.WriteAllText(path, commondir);
        Deny("git checkout main", _main);
        DenyFile("Write", Path.Combine(_main, "src", "a.cs"), _wt);
    }

    [Fact]
    public void Worktree_with_commondir_to_the_common_git_dir_stays_allowed()
    {
        Allow("git commit -m x", _wt);
        Assert.NotEqual(PolicyRules.ProtectMainCheckout, RunFile("Write", Path.Combine(_wt, "src", "a.cs"), _wt).Rule);
    }

    private const string SessionReason = "Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu";

    private PolicyDecision RunInSession(string? sessionRoot, ToolCall call) =>
        ToolCallPolicy.Evaluate(call, Ctx() with { SessionRoot = sessionRoot });

    /// <summary>Q-0209 (Option B): Startwurzel im Haupt-Checkout sperrt Bash und alle Datei-Schreibwerkzeuge, auch mit cwd in der Worktree.</summary>
    [Theory]
    [InlineData("Bash")]
    [InlineData("Write")]
    [InlineData("Edit")]
    [InlineData("MultiEdit")]
    [InlineData("NotebookEdit")]
    public void Session_started_in_the_main_checkout_denies_bash_and_file_writers(string tool)
    {
        var call = tool == "Bash" ? new ToolCall("Bash", Command: "ls", Cwd: _wt) : new ToolCall(tool, FilePath: Path.Combine(_wt, "src", "a.cs"), Cwd: _wt);
        var d = RunInSession(_main, call);
        Assert.Equal(PolicyDecisionKind.Deny, d.Kind);
        Assert.Equal(PolicyRules.ProtectMainCheckout, d.Rule);
        Assert.Equal(SessionReason, d.Reason);
        var p = ToolCallPolicy.EvaluateProtection(call, Ctx() with { SessionRoot = _main });
        Assert.Equal(PolicyDecisionKind.Deny, p.Kind);
        Assert.Equal(SessionReason, ToolCallPolicy.MainCheckoutSession(tool, _main)?.Reason);
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Grep")]
    [InlineData("Glob")]
    public void Session_started_in_the_main_checkout_still_reads(string tool)
    {
        var d = RunInSession(_main, new ToolCall(tool, FilePath: Path.Combine(_main, "src", "a.cs"), Cwd: _wt));
        Assert.NotEqual(PolicyRules.ProtectMainCheckout, d.Rule);
        Assert.Null(ToolCallPolicy.MainCheckoutSession(tool, _main));
    }

    [Fact]
    public void Session_root_below_or_through_a_symlink_into_the_main_checkout_denies()
    {
        Link(Path.Combine(_tmp, "lnk-root"), _main);
        Assert.Equal(SessionReason, RunInSession(Path.Combine(_tmp, "lnk-root"), new ToolCall("Bash", Command: "ls", Cwd: _wt)).Reason);
        Assert.Equal(SessionReason, RunInSession(Path.Combine(_main, "src"), new ToolCall("Bash", Command: "ls", Cwd: _wt)).Reason);
    }

    [Fact]
    public void Session_started_in_a_worktree_or_without_root_is_not_blocked_by_option_b()
    {
        Assert.NotEqual(SessionReason, RunInSession(_wt, new ToolCall("Bash", Command: "ls", Cwd: _wt)).Reason);
        Assert.NotEqual(SessionReason, RunInSession(null, new ToolCall("Bash", Command: "ls", Cwd: _wt)).Reason);
        Assert.NotEqual(SessionReason, RunInSession("", new ToolCall("Bash", Command: "ls", Cwd: _wt)).Reason);
        Assert.NotEqual(SessionReason, RunInSession(_plain, new ToolCall("Bash", Command: "ls", Cwd: _plain)).Reason);
        Assert.Null(ToolCallPolicy.MainCheckoutSession("Bash", _wt));
    }

    [Fact]
    public void Write_outside_any_repo_is_allowed()
    {
        var d = RunFile("Write", Path.Combine(_tmp, "scratch", "a.txt"), _wt);
        Assert.NotEqual(PolicyRules.ProtectMainCheckout, d.Rule);
        Assert.NotEqual(PolicyRules.ProtectMainCheckout, RunFile("Write", Path.Combine(_plain, "src", "a.cs"), _wt).Rule);
    }
}
