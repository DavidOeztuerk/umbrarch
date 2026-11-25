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

    private (int Code, string Out, string Err) Run(string dir, bool claude, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var k in psi.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal) || k == "CLAUDECODE").ToList()) psi.Environment.Remove(k);
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        psi.Environment["GIT_CONFIG_GLOBAL"] = _empty;
        psi.Environment["GIT_AUTHOR_NAME"] = psi.Environment["GIT_COMMITTER_NAME"] = "t";
        psi.Environment["GIT_AUTHOR_EMAIL"] = psi.Environment["GIT_COMMITTER_EMAIL"] = "t@t";
        if (claude) psi.Environment["CLAUDECODE"] = "1";
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
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
    public void Documents_worktree_add_with_new_branch_from_the_main_checkout_with_CLAUDECODE()
    {
        var path = Path.Combine(_main, ".claude", "worktrees", "G-9");
        var r = Run(_main, true, "worktree", "add", path, "-b", "monarch/G-9-x", "origin/develop");
        Assert.NotEqual(0, r.Code);
        Assert.Contains("ADR-0020", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void Documents_worktree_add_with_new_branch_from_a_worktree_with_CLAUDECODE()
    {
        var path = Path.Combine(_main, ".claude", "worktrees", "G-9");
        var r = Run(_wt, true, "-C", _wt, "worktree", "add", path, "-b", "monarch/G-9-x", "origin/develop");
        Assert.True(r.Code == 0, $"verweigert: {r.Err}");
    }
}
