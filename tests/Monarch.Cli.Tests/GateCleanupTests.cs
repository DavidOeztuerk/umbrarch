using System.Diagnostics;
using System.Text.Json;
using Monarch.Infrastructure.Ledger;

namespace Monarch.Cli.Tests;

/// <summary>Q-0197 / ADR-0020 §6: <c>gate cleanup</c> commits the Monarch state on the gate branch, pushes it and removes the worktree.</summary>
public class GateCleanupTests
{
    private const string Merged = """{"gate":"G-0001","pr":7,"merge_commit_sha":"abc"}""";
    private const string Green = """{"gate":"G-0001","result":"green"}""";
    private const string Red = """{"gate":"G-0001","result":"red"}""";
    private const string Skip = "G-0001: Monarch-Stand nach Merge [skip ci]";

    /// <summary>Bare origin plus a main checkout whose only linked worktree is <c>monarch/G-0001-x</c>.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly string _tmp = Path.Combine(Path.GetTempPath(), "monarch-clean-" + Guid.NewGuid().ToString("N"));

        public Rig()
        {
            Directory.CreateDirectory(_tmp);
            Bare = Path.Combine(_tmp, "remote.git");
            Directory.CreateDirectory(Bare);
            Git(Bare, "init", "-q", "--bare", "-b", "develop");
            P = new TempProject(systemYaml: "mana:\n  plan: pro\n");
            Git(P.Root, "init", "-q", "-b", "develop");
            Git(P.Root, "config", "user.email", "t@t");
            Git(P.Root, "config", "user.name", "t");
            Git(P.Root, "config", "commit.gpgsign", "false");
            File.WriteAllText(Path.Combine(P.Root, ".gitignore"), ".monarch/local/\n.claude/\n");
            Git(P.Root, "add", "-A");
            Git(P.Root, "commit", "-q", "-m", "init");
            Git(P.Root, "remote", "add", "origin", Bare);
            Git(P.Root, "push", "-q", "origin", "develop");
            Git(P.Root, "worktree", "add", "-q", ".claude/worktrees/G-0001", "-b", "monarch/G-0001-x", "develop");
            Wt = Path.Combine(P.Root, ".claude", "worktrees", "G-0001");
            Assert.True(File.Exists(Path.Combine(Wt, ".monarch", "system.yaml")));
        }

        public TempProject P { get; }

        public string Bare { get; }

        public string Wt { get; }

        /// <summary>Appends one ledger row in the worktree after moving the clock forward one minute.</summary>
        public void Seed(string type, string json)
        {
            P.Clock.UtcNow = P.Clock.UtcNow.AddMinutes(1);
            var store = new JsonlLedgerStore(new JsonlLedgerOptions { Root = Wt }, P.Clock);
            store.Append("s9", "cli", type, JsonDocument.Parse(json).RootElement.Clone());
        }

        public string? RemoteTip()
        {
            var r = GitDir("rev-parse", "refs/heads/monarch/G-0001-x");
            return r.Code == 0 ? r.Out : null;
        }

        public string RemoteSubject() => GitDir("log", "-1", "--format=%s", "refs/heads/monarch/G-0001-x").Out;

        public string WorktreeList()
        {
            var psi = Start(P.Root);
            psi.ArgumentList.Add("worktree");
            psi.ArgumentList.Add("list");
            return Finish(psi).Out;
        }

        /// <summary>Counts <c>GateCleanedUp</c> lines on the gate branch in the bare remote.</summary>
        public (int Code, string Out) GrepCleaned()
        {
            var r = GitDir("grep", "-c", "GateCleanedUp", "refs/heads/monarch/G-0001-x", "--", ".monarch/ledger");
            return (r.Code, r.Out);
        }

        public void Dispose()
        {
            P.Dispose();
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

        private (int Code, string Out, string Err) GitDir(params string[] args)
        {
            var psi = Start(_tmp);
            psi.ArgumentList.Add("--git-dir");
            psi.ArgumentList.Add(Bare);
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            return Finish(psi);
        }
    }

    private static void Git(string dir, params string[] args)
    {
        var psi = Start(dir);
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        var r = Finish(psi);
        Assert.True(r.Code == 0, $"git {string.Join(' ', args)} failed: {r.Err}");
    }

    private static ProcessStartInfo Start(string dir)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-c", "user.email=t@t", "-c", "user.name=t", "-c", "commit.gpgsign=false" })
        {
            psi.ArgumentList.Add(a);
        }

        return psi;
    }

    private static (int Code, string Out, string Err) Finish(ProcessStartInfo psi)
    {
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output.Trim(), err.Result.Trim());
    }

    private static void MergedAndGreen(Rig rig)
    {
        rig.Seed("GateMerged", Merged);
        rig.Seed("IntegrationRunChecked", Green);
    }

    /// <summary>Without GateMerged the branch must stay unpublished and the worktree must stay.</summary>
    [Fact]
    public void Refuses_without_GateMerged()
    {
        using var rig = new Rig();
        var r = rig.P.Run(["gate", "cleanup", "G-0001"]);
        Assert.Equal(1, r.Code);
        Assert.Contains("no GateMerged", r.Out + r.Err, StringComparison.Ordinal);
        Assert.Null(rig.RemoteTip());
        Assert.True(Directory.Exists(rig.Wt));
    }

    /// <summary>A red check after the merge is not the green check the cleanup requires.</summary>
    [Fact]
    public void Refuses_without_a_green_check_after_the_merge()
    {
        using var rig = new Rig();
        rig.Seed("GateMerged", Merged);
        rig.Seed("IntegrationRunChecked", Red);
        var r = rig.P.Run(["gate", "cleanup", "G-0001"]);
        Assert.Equal(1, r.Code);
        Assert.Contains("gate check", r.Out + r.Err, StringComparison.Ordinal);
        Assert.Null(rig.RemoteTip());
    }

    /// <summary>A green check from before GateMerged does not cover this merge.</summary>
    [Fact]
    public void Refuses_a_green_check_from_before_the_merge()
    {
        using var rig = new Rig();
        rig.Seed("IntegrationRunChecked", Green);
        rig.Seed("GateMerged", Merged);
        var r = rig.P.Run(["gate", "cleanup", "G-0001"]);
        Assert.Equal(1, r.Code);
    }

    /// <summary>A cleanup started inside the worktree would let the hook keep appending there.</summary>
    [Fact]
    public void Refuses_from_inside_the_worktree()
    {
        using var rig = new Rig();
        MergedAndGreen(rig);
        var r = rig.P.Run(["gate", "cleanup", "G-0001"], cwd: rig.Wt);
        Assert.Equal(1, r.Code);
        Assert.Contains("main checkout", r.Out + r.Err, StringComparison.Ordinal);
        Assert.Null(rig.RemoteTip());
    }

    /// <summary>Shadows must not push or delete a worktree.</summary>
    [Fact]
    public void Refuses_in_a_shadow()
    {
        using var rig = new Rig();
        var r = rig.P.Run(["gate", "cleanup", "G-0001"], vars: new Dictionary<string, string?> { ["MONARCH_AGENT_TYPE"] = "knight" });
        Assert.Equal(1, r.Code);
    }

    /// <summary>The happy path is one skip-marker commit on the gate branch and a removed worktree.</summary>
    [Fact]
    public void Boss_commits_with_skip_marker_pushes_the_branch_and_removes_the_worktree()
    {
        using var rig = new Rig();
        MergedAndGreen(rig);
        var r = rig.P.Run(["gate", "cleanup", "G-0001"]);
        Assert.Equal(0, r.Code);
        Assert.Equal(Skip, rig.RemoteSubject());
        var grep = rig.GrepCleaned();
        Assert.Equal(0, grep.Code);
        Assert.Contains(":1", grep.Out, StringComparison.Ordinal);
        Assert.False(Directory.Exists(rig.Wt));
        Assert.DoesNotContain(rig.Wt, rig.WorktreeList(), StringComparison.Ordinal);
    }

    /// <summary>An untracked file blocks removal; the reply names the file and never suggests a forced remove.</summary>
    [Fact]
    public void Untracked_file_keeps_the_worktree_names_it_and_never_suggests_force()
    {
        using var rig = new Rig();
        MergedAndGreen(rig);
        Directory.CreateDirectory(Path.Combine(rig.Wt, "src"));
        File.WriteAllText(Path.Combine(rig.Wt, "src", "x.txt"), "x");
        var r = rig.P.Run(["gate", "cleanup", "G-0001"]);
        Assert.Equal(1, r.Code);
        var text = r.Out + r.Err;
        Assert.Contains("src/x.txt", text, StringComparison.Ordinal);
        Assert.DoesNotContain("force", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(rig.Wt));
        Assert.Equal(Skip, rig.RemoteSubject());
    }

    /// <summary>After the blocking file is gone, a second call removes the worktree and does not commit again.</summary>
    [Fact]
    public void Second_call_after_the_fix_removes_the_worktree_without_a_new_commit()
    {
        using var rig = new Rig();
        MergedAndGreen(rig);
        Directory.CreateDirectory(Path.Combine(rig.Wt, "src"));
        File.WriteAllText(Path.Combine(rig.Wt, "src", "x.txt"), "x");
        Assert.Equal(1, rig.P.Run(["gate", "cleanup", "G-0001"]).Code);
        var tip = rig.RemoteTip();
        File.Delete(Path.Combine(rig.Wt, "src", "x.txt"));
        var again = rig.P.Run(["gate", "cleanup", "G-0001"]);
        Assert.Equal(0, again.Code);
        Assert.Equal(tip, rig.RemoteTip());
        Assert.False(Directory.Exists(rig.Wt));
    }
}
