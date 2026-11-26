using System.Diagnostics;

namespace Monarch.Cli.Tests;

/// <summary>ADR-0020 §1, Schicht 2: <c>plugin/git-hooks/reference-transaction</c> gegen echte Git-Repos (Remote, Haupt-Checkout, Worktree).</summary>
public sealed class ReferenceTransactionHookTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "monarch-rt-" + Guid.NewGuid().ToString("N"));
    private readonly string _empty;
    private readonly string _bare;
    private readonly string _main;
    private readonly string _wt;

    public ReferenceTransactionHookTests()
    {
        Directory.CreateDirectory(_tmp);
        _empty = Path.Combine(_tmp, "gitconfig-empty");
        File.WriteAllText(_empty, "");
        _bare = Path.Combine(_tmp, "remote.git");
        _main = Path.Combine(_tmp, "main");
        Directory.CreateDirectory(_bare);
        Directory.CreateDirectory(_main);
        Setup(_bare, "init", "-q", "--bare", "-b", "develop");
        Setup(_main, "init", "-q", "-b", "develop");
        File.WriteAllText(Path.Combine(_main, "a.txt"), "a\n");
        Setup(_main, "add", "-A");
        Setup(_main, "commit", "-q", "-m", "init");
        Setup(_main, "branch", "main");
        Setup(_main, "remote", "add", "origin", _bare);
        Setup(_main, "push", "-q", "origin", "develop", "main");
        Setup(_main, "fetch", "-q", "origin");
        _wt = Path.Combine(_main, ".claude", "worktrees", "G-1");
        Setup(_main, "worktree", "add", "-q", _wt, "-b", "monarch/G-1", "develop");
        Install();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tmp, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string RepoFile(params string[] parts)
    {
        var d = AppContext.BaseDirectory;
        while (d is not null && !Directory.Exists(Path.Combine(d, "plugin", "git-hooks"))) d = Path.GetDirectoryName(d);
        Assert.NotNull(d);
        return Path.Combine([d!, .. parts]);
    }

    private void Install()
    {
        var dest = Path.Combine(_main, ".git", "hooks", "reference-transaction");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(RepoFile("plugin", "git-hooks", "reference-transaction"), dest, true);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dest, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private (int Code, string Out, string Err) Run(string dir, bool claude, params string[] args) => RunWithInput(dir, claude, null, args);

    private (int Code, string Out, string Err) RunWithInput(string dir, bool claude, string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin is not null };
        foreach (var k in psi.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal) || k == "CLAUDECODE").ToList()) psi.Environment.Remove(k);
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_CONFIG_GLOBAL"] = _empty;
        psi.Environment["GIT_AUTHOR_NAME"] = psi.Environment["GIT_COMMITTER_NAME"] = "t";
        psi.Environment["GIT_AUTHOR_EMAIL"] = psi.Environment["GIT_COMMITTER_EMAIL"] = "t@t";
        if (claude) psi.Environment["CLAUDECODE"] = "1";
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        if (stdin is not null)
        {
            p.StandardInput.Write(stdin);
            p.StandardInput.Close();
        }
        var err = p.StandardError.ReadToEndAsync();
        var o = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, o.Trim(), err.Result.Trim());
    }

    private void Setup(string dir, params string[] args)
    {
        var r = Run(dir, false, args);
        Assert.True(r.Code == 0, $"git {string.Join(' ', args)} failed: {r.Err}");
    }

    private string Rev(string dir, string rev) => Run(dir, false, "rev-parse", rev).Out;

    private void Change(string dir, string file)
    {
        File.WriteAllText(Path.Combine(dir, file), Guid.NewGuid().ToString("N"));
        Setup(dir, "add", "-A");
    }

    [Fact]
    public void Commit_in_the_main_checkout_is_refused_and_HEAD_stays()
    {
        var before = Rev(_main, "HEAD");
        Change(_main, "b.txt");
        var r = Run(_main, true, "commit", "-q", "-m", "x");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal(before, Rev(_main, "HEAD"));
    }

    [Fact]
    public void Commit_in_the_worktree_is_allowed()
    {
        var before = Rev(_wt, "HEAD");
        Change(_wt, "w.txt");
        var r = Run(_wt, true, "commit", "-q", "-m", "x");
        Assert.True(r.Code == 0, r.Err);
        Assert.NotEqual(before, Rev(_wt, "HEAD"));
    }

    [Fact]
    public void Branch_force_develop_from_the_worktree_is_refused()
    {
        Change(_wt, "w.txt");
        Setup(_wt, "commit", "-q", "-m", "w");
        Setup(_main, "checkout", "-q", "--detach"); // sonst verweigert git selbst (develop ausgecheckt)
        var develop = Rev(_main, "refs/heads/develop");
        var r = Run(_wt, true, "branch", "-f", "develop", Rev(_wt, "HEAD"));
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal(develop, Rev(_main, "refs/heads/develop"));
    }

    // Auf macOS (APFS ohne Beachtung der Schreibweise) ist refs/heads/Develop dieselbe Datei wie refs/heads/develop.
    [Theory]
    [InlineData("refs/heads/Develop")]
    [InlineData("refs/heads/MAIN")]
    [InlineData("refs/HEADS/develop")]
    public void Protected_refs_in_another_spelling_are_refused(string reference)
    {
        Change(_wt, "w.txt");
        Setup(_wt, "commit", "-q", "-m", "w");
        Setup(_main, "checkout", "-q", "--detach");
        var r = Run(_wt, true, "update-ref", reference, Rev(_wt, "HEAD"));
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void Branch_delete_of_a_packed_develop_from_the_worktree_is_refused()
    {
        Setup(_main, "checkout", "-q", "--detach");
        Setup(_main, "pack-refs", "--all");
        Assert.False(File.Exists(Path.Combine(_main, ".git", "refs", "heads", "develop")));
        Assert.Contains("refs/heads/develop", File.ReadAllText(Path.Combine(_main, ".git", "packed-refs")), StringComparison.Ordinal);
        var r = Run(_wt, true, "branch", "-D", "develop");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal(0, Run(_main, false, "rev-parse", "--verify", "-q", "refs/heads/develop").Code);
    }

    [Fact]
    public void Fetch_from_the_worktree_is_allowed()
    {
        var r = Run(_wt, true, "fetch", "origin");
        Assert.True(r.Code == 0, r.Err);
    }

    [Fact]
    public void Without_CLAUDECODE_commit_in_the_main_checkout_is_allowed()
    {
        Change(_main, "b.txt");
        var r = Run(_main, false, "commit", "-q", "-m", "x");
        Assert.True(r.Code == 0, r.Err);
    }

    [Fact]
    public void Without_CLAUDECODE_branch_force_develop_is_allowed()
    {
        Change(_wt, "w.txt");
        Setup(_wt, "commit", "-q", "-m", "w");
        var tip = Rev(_wt, "HEAD");
        Setup(_main, "checkout", "-q", "--detach");
        var r = Run(_wt, false, "branch", "-f", "develop", tip);
        Assert.True(r.Code == 0, r.Err);
        Assert.Equal(tip, Rev(_main, "refs/heads/develop"));
    }

    [Fact]
    public void Without_CLAUDECODE_branch_delete_of_a_packed_develop_is_allowed()
    {
        Setup(_main, "checkout", "-q", "--detach");
        Setup(_main, "pack-refs", "--all");
        var r = Run(_wt, false, "branch", "-D", "develop");
        Assert.True(r.Code == 0, r.Err);
        Assert.NotEqual(0, Run(_main, false, "rev-parse", "--verify", "-q", "refs/heads/develop").Code);
    }

    [Fact]
    public void Documents_worktree_remove_from_the_main_checkout_with_CLAUDECODE()
    {
        var r = Run(_main, true, "worktree", "remove", _wt);
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
        Assert.False(Directory.Exists(_wt));
    }

    [Fact]
    public void Worktree_add_with_new_branch_from_the_main_checkout_is_allowed()
    {
        var path = Path.Combine(_main, ".claude", "worktrees", "G-9");
        var r = Run(_main, true, "worktree", "add", path, "-b", "monarch/G-9-x", "origin/develop");
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
        Assert.Equal(0, Run(_main, false, "rev-parse", "--verify", "-q", "refs/heads/monarch/G-9-x").Code);
        Assert.True(Directory.Exists(path));
    }

    private string HeadRef(string dir) => Run(dir, false, "symbolic-ref", "-q", "HEAD").Out;

    [Fact]
    public void Checkout_of_another_branch_in_the_main_checkout_is_refused_and_HEAD_stays()
    {
        Setup(_main, "branch", "other");
        var r = Run(_main, true, "checkout", "-q", "other");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal("refs/heads/develop", HeadRef(_main));
    }

    // Eine vorgetäuschte Lock-Datei einer Worktree darf das HEAD des Haupt-Checkouts nicht freigeben: git sperrt dabei .git/HEAD.lock.
    [Fact]
    public void Checkout_in_the_main_checkout_is_refused_even_with_a_planted_worktree_HEAD_lock()
    {
        Setup(_main, "branch", "other");
        File.WriteAllText(Path.Combine(_main, ".git", "worktrees", "G-1", "HEAD.lock"), "");
        var r = Run(_main, true, "checkout", "-q", "other");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal("refs/heads/develop", HeadRef(_main));
    }

    [Fact]
    public void Switch_to_another_branch_in_the_main_checkout_is_refused_and_HEAD_stays()
    {
        Setup(_main, "branch", "other");
        var r = Run(_main, true, "switch", "-q", "other");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal("refs/heads/develop", HeadRef(_main));
    }

    [Fact]
    public void Checkout_new_branch_in_the_main_checkout_is_refused_and_HEAD_stays()
    {
        var r = Run(_main, true, "checkout", "-q", "-b", "monarch/G-9-w");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal("refs/heads/develop", HeadRef(_main));
    }

    [Fact]
    public void Checkout_detach_in_the_main_checkout_is_refused_and_HEAD_stays()
    {
        var r = Run(_main, true, "checkout", "-q", "--detach");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.Equal("refs/heads/develop", HeadRef(_main));
    }

    [Fact]
    public void Update_ref_of_the_checked_out_non_protected_branch_in_the_main_checkout_is_refused()
    {
        Setup(_main, "checkout", "-q", "-b", "feature");
        var sha = Rev(_main, "refs/heads/develop");
        var r = Run(_main, true, "update-ref", "refs/heads/feature", sha);
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_ref_of_the_checked_out_branch_in_the_main_checkout_in_another_spelling_is_refused()
    {
        Setup(_main, "checkout", "-q", "-b", "feature");
        var sha = Rev(_main, "refs/heads/develop");
        var r = Run(_main, true, "update-ref", "refs/heads/Feature", sha);
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void Documents_update_ref_of_the_main_checkouts_branch_from_a_worktree_is_allowed()
    {
        Setup(_main, "checkout", "-q", "-b", "feature");
        Change(_wt, "w.txt");
        Setup(_wt, "commit", "-q", "-m", "w");
        var tip = Rev(_wt, "HEAD");
        var r = Run(_wt, true, "-C", _wt, "update-ref", "refs/heads/feature", tip);
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
        Assert.Equal(tip, Rev(_main, "refs/heads/feature"));
    }

    [Fact]
    public void Branch_create_in_the_main_checkout_is_allowed()
    {
        var r = Run(_main, true, "branch", "monarch/G-9-y");
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
        Assert.Equal(0, Run(_main, false, "rev-parse", "--verify", "-q", "refs/heads/monarch/G-9-y").Code);
    }

    [Fact]
    public void Update_ref_with_a_new_branch_in_the_main_checkout_is_allowed()
    {
        var r = Run(_main, true, "update-ref", "refs/heads/monarch/G-9-z", Rev(_main, "refs/heads/develop"));
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
    }

    [Fact]
    public void Transaction_with_an_allowed_and_a_protected_ref_is_refused_as_a_whole()
    {
        Change(_wt, "w.txt");
        Setup(_wt, "commit", "-q", "-m", "w");
        Setup(_main, "checkout", "-q", "--detach");
        var tip = Rev(_wt, "HEAD");
        var develop = Rev(_main, "refs/heads/develop");
        var input = $"start\nupdate refs/heads/monarch/G-9-ok {tip}\nupdate refs/heads/develop {tip}\nprepare\ncommit\n";
        var r = RunWithInput(_wt, true, input, "update-ref", "--stdin");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
        Assert.NotEqual(0, Run(_main, false, "rev-parse", "--verify", "-q", "refs/heads/monarch/G-9-ok").Code);
        Assert.Equal(develop, Rev(_main, "refs/heads/develop"));
    }

    [Fact]
    public void Documents_worktree_add_with_new_branch_from_a_worktree_with_CLAUDECODE()
    {
        var path = Path.Combine(_main, ".claude", "worktrees", "G-9");
        var r = Run(_wt, true, "-C", _wt, "worktree", "add", path, "-b", "monarch/G-9-x", "origin/develop");
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
    }
}
