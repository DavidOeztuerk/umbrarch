using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Monarch.Cli.Tests;

/// <summary>Q-0197 / ADR-0020 §4: <c>gate carry</c> copies only <c>state</c> from merged gate branches, and it does not commit.</summary>
public class GateCarryTests
{
    private static readonly Regex StateLine = new(@"^state: (\S+)$", RegexOptions.Multiline);

    /// <summary>Main checkout on develop, with optional gate branches on a bare origin.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly string _tmp = Path.Combine(Path.GetTempPath(), "monarch-carry-" + Guid.NewGuid().ToString("N"));

        public Rig(bool branches = true)
        {
            Directory.CreateDirectory(_tmp);
            var bare = Path.Combine(_tmp, "remote.git");
            Directory.CreateDirectory(bare);
            Git(bare, "init", "-q", "--bare", "-b", "develop");
            P = new TempProject(systemYaml: "mana:\n  plan: pro\n");
            Gate1 = Seed.Gate(P, "G-0001", "[Q-0001, Q-0002]");
            Q1 = Seed.Quest(P, "Q-0001", state: "in_review", gate: "G-0001");
            Q2 = Seed.Quest(P, "Q-0002", state: "in_review", gate: "G-0001");
            Gate2 = Seed.Gate(P, "G-0002", "[Q-0003]");
            Q3 = Seed.Quest(P, "Q-0003", state: "in_review", gate: "G-0002");
            Q4 = Seed.Quest(P, "Q-0004", state: "in_review", gate: "G-0001");
            File.WriteAllText(Path.Combine(P.Root, ".gitignore"), ".monarch/local/\n.claude/\n");
            Git(P.Root, "init", "-q", "-b", "develop");
            Git(P.Root, "add", "-A");
            Git(P.Root, "commit", "-q", "-m", "init");
            Git(P.Root, "remote", "add", "origin", bare);
            Git(P.Root, "push", "-q", "origin", "develop");
            if (!branches)
            {
                AddWorktree();
                return;
            }

            Git(P.Root, "checkout", "-q", "-b", "monarch/G-0001-x");
            Replace(Gate1, "state: proposed", "state: merged");
            Replace(Q1, "state: in_review", "state: cleared");
            Replace(Q1, "rank: C", "rank: A");
            Replace(Q2, "state: in_review", "state: bogus");
            Replace(Q4, "state: in_review", "state: cleared");
            var foreign = Path.Combine(P.Root, ".monarch", "ledger", "2026-11", "foreign.abcdef12.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
            File.WriteAllText(foreign, "{}\n");
            Git(P.Root, "add", "-A");
            Git(P.Root, "commit", "-q", "-m", "merged");
            Git(P.Root, "push", "-q", "origin", "monarch/G-0001-x");

            Git(P.Root, "checkout", "-q", "develop");
            Git(P.Root, "checkout", "-q", "-b", "monarch/G-0002-y");
            File.AppendAllText(Gate2, "extra\n");
            Git(P.Root, "add", "-A");
            Git(P.Root, "commit", "-q", "-m", "note");
            Git(P.Root, "push", "-q", "origin", "monarch/G-0002-y");

            Git(P.Root, "checkout", "-q", "develop");
            Git(P.Root, "checkout", "-q", "-b", "monarch/G-0009-z");
            File.WriteAllText(Path.Combine(P.Root, ".monarch", "gates", "G-0009-z.md"), "---\nid: G-0009\nstate: merged\n---\n# G-0009\n");
            Git(P.Root, "add", "-A");
            Git(P.Root, "commit", "-q", "-m", "foreign gate");
            Git(P.Root, "push", "-q", "origin", "monarch/G-0009-z");

            Git(P.Root, "checkout", "-q", "develop");
            var listed = Out(P.Root, "for-each-ref", "refs/remotes/origin/monarch/");
            if (!listed.Contains("monarch/G-0001-x", StringComparison.Ordinal)
                || !listed.Contains("monarch/G-0002-y", StringComparison.Ordinal)
                || !listed.Contains("monarch/G-0009-z", StringComparison.Ordinal))
            {
                Git(P.Root, "fetch", "-q", "origin");
            }

            AddWorktree();
        }

        public TempProject P { get; }

        /// <summary>The linked worktree off develop in which <c>gate carry</c> runs; all file checks read here.</summary>
        public string Wt { get; private set; } = "";

        public string Gate1 { get; private set; }

        public string Gate2 { get; private set; }

        public string Q1 { get; private set; }

        public string Q2 { get; private set; }

        public string Q3 { get; private set; }

        public string Q4 { get; private set; }

        public string Text(string path) => File.ReadAllText(path);

        public string StateOf(string path) => StateLine.Match(Text(path)).Groups[1].Value;

        public string Head() => Out(Wt, "rev-parse", "HEAD");

        public string Status() => Out(Wt, "status", "--short");

        public Dictionary<string, string> BoardFiles()
        {
            var root = Path.Combine(Wt, ".monarch");
            return Directory.GetFiles(Path.Combine(root, "gates")).Concat(Directory.GetFiles(Path.Combine(root, "quests")))
                .ToDictionary(f => f, File.ReadAllText, StringComparer.Ordinal);
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

        private void AddWorktree()
        {
            Git(P.Root, "worktree", "add", "-q", ".claude/worktrees/W", "-b", "work", "develop");
            Wt = Path.Combine(P.Root, ".claude", "worktrees", "W");
            Gate1 = InWt(Gate1);
            Gate2 = InWt(Gate2);
            Q1 = InWt(Q1);
            Q2 = InWt(Q2);
            Q3 = InWt(Q3);
            Q4 = InWt(Q4);
        }

        private string InWt(string path) => Path.Combine(Wt, Path.GetRelativePath(P.Root, path));

        private static void Replace(string path, string oldText, string newText) =>
            File.WriteAllText(path, File.ReadAllText(path).Replace(oldText, newText, StringComparison.Ordinal));
    }

    private static void Git(string dir, params string[] args)
    {
        var r = Run(dir, args);
        Assert.True(r.Code == 0, $"git {string.Join(' ', args)} failed: {r.Err}");
    }

    private static string Out(string dir, params string[] args)
    {
        var r = Run(dir, args);
        Assert.True(r.Code == 0, $"git {string.Join(' ', args)} failed: {r.Err}");
        return r.Out;
    }

    private static (int Code, string Out, string Err) Run(string dir, string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-c", "user.email=t@t", "-c", "user.name=t", "-c", "commit.gpgsign=false" }.Concat(args))
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output.Trim(), err.Result.Trim());
    }

    private static string ExceptState(string text) => StateLine.Replace(text, "state: *");

    /// <summary>Reads the ledger of the worktree, where carry appends its event.</summary>
    private static List<(string Type, System.Text.Json.JsonElement Payload)> Carried(Rig rig)
    {
        var list = new List<(string, System.Text.Json.JsonElement)>();
        var dir = Path.Combine(rig.Wt, ".monarch", "ledger");
        if (!Directory.Exists(dir))
        {
            return list;
        }

        foreach (var f in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            foreach (var line in File.ReadAllLines(f))
            {
                var doc = System.Text.Json.JsonDocument.Parse(line).RootElement;
                if (doc.GetProperty("type").GetString() == "GateStatusCarried")
                {
                    list.Add(("GateStatusCarried", doc.GetProperty("payload").Clone()));
                }
            }
        }

        return list;
    }

    /// <summary>Only the merged gate's state and a valid listed quest state move; rank, bogus, outsiders and ledger files stay behind.</summary>
    [Fact]
    public void Boss_carry_takes_only_state_of_a_merged_gate_and_its_listed_quests()
    {
        using var rig = new Rig();
        var before = rig.Text(rig.Q1);
        var gate2 = rig.Text(rig.Gate2);
        var head = rig.Head();
        var r = rig.P.Run(["gate", "carry"], cwd: rig.Wt);
        Assert.Equal(0, r.Code);
        Assert.Equal("merged", rig.StateOf(rig.Gate1));
        Assert.Equal("cleared", rig.StateOf(rig.Q1));
        Assert.Equal(ExceptState(before), ExceptState(rig.Text(rig.Q1)));
        Assert.Contains("rank: C", rig.Text(rig.Q1), StringComparison.Ordinal);
        Assert.Equal("in_review", rig.StateOf(rig.Q2));
        Assert.Equal("in_review", rig.StateOf(rig.Q4));
        Assert.Equal(gate2, rig.Text(rig.Gate2));
        Assert.Equal("proposed", rig.StateOf(rig.Gate2));
        Assert.Empty(Directory.GetFiles(Path.Combine(rig.Wt, ".monarch", "gates"), "G-0009*"));
        Assert.False(File.Exists(Path.Combine(rig.Wt, ".monarch", "ledger", "2026-11", "foreign.abcdef12.jsonl")));
        var carried = Assert.Single(Carried(rig));
        Assert.Equal("G-0001", carried.Payload.GetProperty("gate").GetString());
        Assert.Equal("Q-0001", carried.Payload.GetProperty("quests").GetString());
        Assert.Equal("refs/remotes/origin/monarch/G-0001-x", carried.Payload.GetProperty("from_ref").GetString());
        Assert.Equal(head, rig.Head());
        var changed = rig.Status().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()[2..].Trim()).Order(StringComparer.Ordinal).ToList();
        Assert.Equal([".monarch/gates/G-0001-g.md", ".monarch/ledger/", ".monarch/quests/Q-0001-t.md"], changed);
    }

    /// <summary>A gate already marked merged is not carried again.</summary>
    [Fact]
    public void Second_call_changes_nothing()
    {
        using var rig = new Rig();
        Assert.Equal(0, rig.P.Run(["gate", "carry"], cwd: rig.Wt).Code);
        var files = rig.BoardFiles();
        var again = rig.P.Run(["gate", "carry"], cwd: rig.Wt);
        Assert.Equal(0, again.Code);
        Assert.Contains("nothing to carry", again.Out + again.Err, StringComparison.Ordinal);
        Assert.Single(Carried(rig));
        foreach (var (path, text) in files)
        {
            Assert.Equal(text, File.ReadAllText(path));
        }
    }

    /// <summary>A quest state that already matches is left alone, and the gate file is still finished.</summary>
    [Fact]
    public void Interrupted_carry_completes_on_the_second_call()
    {
        using var rig = new Rig();
        File.WriteAllText(rig.Q1, rig.Text(rig.Q1).Replace("state: in_review", "state: cleared", StringComparison.Ordinal));
        Assert.Equal("proposed", rig.StateOf(rig.Gate1));
        var r = rig.P.Run(["gate", "carry"], cwd: rig.Wt);
        Assert.Equal(0, r.Code);
        Assert.Equal("merged", rig.StateOf(rig.Gate1));
        var carried = Assert.Single(Carried(rig));
        Assert.Equal("", carried.Payload.GetProperty("quests").GetString());
    }

    /// <summary>A shadow must not copy branch state into the worktree.</summary>
    [Fact]
    public void Refuses_in_a_shadow()
    {
        using var rig = new Rig();
        var files = rig.BoardFiles();
        var head = rig.Head();
        var r = rig.P.Run(["gate", "carry"], cwd: rig.Wt, vars: new Dictionary<string, string?> { ["MONARCH_AGENT_TYPE"] = "knight" });
        Assert.Equal(1, r.Code);
        Assert.Contains("main session", r.Out + r.Err, StringComparison.Ordinal);
        Assert.Empty(Carried(rig));
        Assert.Equal(head, rig.Head());
        Assert.Equal("", rig.Status());
        foreach (var (path, text) in files)
        {
            Assert.Equal(text, File.ReadAllText(path));
        }
    }

    /// <summary>ADR-0020 §4: the main checkout is never touched.</summary>
    [Fact]
    public void Refuses_in_the_main_checkout()
    {
        using var rig = new Rig();
        var files = rig.BoardFiles();
        var r = rig.P.Run(["gate", "carry"], cwd: rig.P.Root);
        Assert.Equal(1, r.Code);
        Assert.Contains("main checkout", r.Out + r.Err, StringComparison.Ordinal);
        Assert.Empty(Carried(rig));
        foreach (var (path, text) in files)
        {
            Assert.Equal(text, File.ReadAllText(path));
        }

        Assert.Equal("", Out(rig.P.Root, "status", "--short"));
    }

    /// <summary>No gate branch on origin means there is nothing to copy.</summary>
    [Fact]
    public void Without_gate_branches_there_is_nothing_to_carry()
    {
        using var rig = new Rig(branches: false);
        var r = rig.P.Run(["gate", "carry"], cwd: rig.Wt);
        Assert.Equal(0, r.Code);
        Assert.Contains("nothing to carry", r.Out + r.Err, StringComparison.Ordinal);
        Assert.Empty(Carried(rig));
    }
}
