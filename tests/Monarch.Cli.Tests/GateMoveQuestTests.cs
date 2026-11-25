using System.Text.Json;
using System.Text.Json.Nodes;
using Monarch.Application.Net;
using Monarch.Infrastructure.Boards;

namespace Monarch.Cli.Tests;

public class GateMoveQuestTests
{
    [Fact]
    public async Task Boss_move_quest_updates_both_gates_the_quest_file_and_logs_QuestMoved()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001, Q-0002]");
        Seed.Gate(p, "G-0002", "[Q-0003]");
        var questFile = Seed.Quest(p, "Q-0001", gate: "G-0001");
        Seed.Quest(p, "Q-0002", gate: "G-0001");
        Seed.Quest(p, "Q-0003", gate: "G-0002");
        var body = Body(File.ReadAllText(questFile));

        var run = p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]);

        Assert.Equal(0, run.Code);
        var board = await ReadBoard(p);
        Assert.Equal(["Q-0002"], QuestsOf(board, "G-0001"));
        Assert.Equal(["Q-0003", "Q-0001"], QuestsOf(board, "G-0002"));
        Assert.Equal("G-0002", QuestGate(board, "Q-0001"));
        var moved = Assert.Single(p.Events(), e => e.Type == "QuestMoved");
        Assert.Equal("Q-0001", moved.Payload.GetProperty("quest").GetString());
        Assert.Equal("G-0001", moved.Payload.GetProperty("from").GetString());
        Assert.Equal("G-0002", moved.Payload.GetProperty("to").GetString());
        Assert.Equal(body, Body(File.ReadAllText(questFile)));
        Assert.Equal(0, p.Run(["ledger", "verify"]).Code);
    }

    [Fact]
    public void Second_call_reports_already_moved_and_logs_nothing()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001, Q-0002]");
        Seed.Gate(p, "G-0002", "[Q-0003]");
        Seed.Quest(p, "Q-0001", gate: "G-0001");
        Seed.Quest(p, "Q-0002", gate: "G-0001");
        Seed.Quest(p, "Q-0003", gate: "G-0002");
        Assert.Equal(0, p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]).Code);

        var again = p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]);

        Assert.Equal(0, again.Code);
        Assert.Contains("already moved", again.Out, StringComparison.Ordinal);
        Assert.Single(p.Events(), e => e.Type == "QuestMoved");
    }

    [Fact]
    public async Task Interrupted_move_is_completed_by_a_second_call()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001]");
        Seed.Gate(p, "G-0002", "[Q-0001]");
        Seed.Quest(p, "Q-0001", gate: "G-0001");

        var run = p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]);

        Assert.Equal(0, run.Code);
        var board = await ReadBoard(p);
        Assert.Equal([], QuestsOf(board, "G-0001"));
        Assert.Equal(["Q-0001"], QuestsOf(board, "G-0002"));
        Assert.Equal("G-0002", QuestGate(board, "Q-0001"));
        var moved = Assert.Single(p.Events(), e => e.Type == "QuestMoved");
        Assert.Equal("G-0001", moved.Payload.GetProperty("from").GetString());
    }

    [Fact]
    public void Accepted_quest_can_be_moved()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001]");
        Seed.Gate(p, "G-0002", "[]");
        Seed.Quest(p, "Q-0001", state: "accepted", gate: "G-0001");

        Assert.Equal(0, p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]).Code);
    }

    [Theory]
    [InlineData("unknown quest")]
    [InlineData("unknown gate")]
    [InlineData("in_progress")]
    [InlineData("cleared")]
    [InlineData("active")]
    public void Refusals_change_nothing(string reason)
    {
        using var p = reason == "active" ? new TempProject(activeRank: "C") : new TempProject();
        var quest = reason == "active" ? "Q-0003" : "Q-0001";
        Seed.Gate(p, "G-0001", $"[{quest}]");
        if (reason != "unknown gate")
        {
            Seed.Gate(p, "G-0002", "[]");
        }

        var state = reason switch
        {
            "in_progress" => "in_progress",
            "cleared" => "cleared",
            "active" => "accepted",
            _ => "proposed",
        };
        if (reason != "unknown quest")
        {
            Seed.Quest(p, quest, state: state, gate: "G-0001");
        }
        else
        {
            Seed.Quest(p, "Q-0001", gate: "G-0001");
        }

        var before = Snapshot(p);
        var target = reason == "unknown gate" ? "G-0099" : "G-0002";
        var id = reason == "unknown quest" ? "Q-0099" : quest;
        var run = p.Run(["gate", "move-quest", id, "--to", target]);

        Assert.Equal(1, run.Code);
        Assert.Equal(before, Snapshot(p));
        Assert.DoesNotContain(p.Events(), e => e.Type == "QuestMoved");
    }

    [Fact]
    public async Task Gate_plan_takes_a_quest_from_its_old_gate_and_logs_QuestMoved()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001]");
        Seed.Quest(p, "Q-0001", state: "proposed", gate: "G-0001");

        var (err, text) = Result(Rpc(p, Call(1, "gate_plan", Plan("Q-0001")))[0]);

        Assert.False(err, text);
        var board = await ReadBoard(p);
        Assert.Equal([], QuestsOf(board, "G-0001"));
        Assert.Contains("Q-0001", QuestsOf(board, "G-0002"), StringComparer.Ordinal);
        Assert.Equal("G-0002", QuestGate(board, "Q-0001"));
        var moved = Assert.Single(p.Events(), e => e.Type == "QuestMoved");
        Assert.Equal("Q-0001", moved.Payload.GetProperty("quest").GetString());
        Assert.Equal("G-0001", moved.Payload.GetProperty("from").GetString());
        Assert.Equal("G-0002", moved.Payload.GetProperty("to").GetString());
        Assert.Equal("mcp", Actor(p, "QuestMoved"));
        Assert.Equal(0, p.Run(["ledger", "verify"]).Code);
    }

    [Fact]
    public void Gate_plan_with_a_new_quest_logs_no_QuestMoved()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001]");
        Seed.Quest(p, "Q-0001", state: "proposed", gate: "G-0001");

        var (err, text) = Result(Rpc(p, Call(1, "gate_plan", Plan("Q-0091")))[0]);

        Assert.False(err, text);
        Assert.DoesNotContain(p.Events(), e => e.Type == "QuestMoved");
        Assert.Equal(0, p.Run(["ledger", "verify"]).Code);
    }

    [Fact]
    public async Task Gate_plan_with_two_quests_from_one_gate_removes_both()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001, Q-0002, Q-0003]");
        Seed.Quest(p, "Q-0001", state: "proposed", gate: "G-0001");
        Seed.Quest(p, "Q-0002", state: "proposed", gate: "G-0001");
        Seed.Quest(p, "Q-0003", state: "proposed", gate: "G-0001");

        var (err, text) = Result(Rpc(p, Call(1, "gate_plan", PlanTwo("Q-0001", "Q-0002")))[0]);

        Assert.False(err, text);
        var board = await ReadBoard(p);
        Assert.Equal(["Q-0003"], QuestsOf(board, "G-0001"));
        var target = QuestsOf(board, "G-0002");
        Assert.Contains("Q-0001", target, StringComparer.Ordinal);
        Assert.Contains("Q-0002", target, StringComparer.Ordinal);
        var moved = p.Events().Where(e => e.Type == "QuestMoved").ToList();
        Assert.Equal(2, moved.Count);
        Assert.All(moved, m => Assert.Equal("G-0001", m.Payload.GetProperty("from").GetString()));
        Assert.Equal(0, p.Run(["ledger", "verify"]).Code);
    }

    [Fact]
    public async Task Repairing_only_the_target_list_logs_no_QuestMoved()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0002", "[Q-0003]");
        Seed.Quest(p, "Q-0001", gate: "G-0002");
        Seed.Quest(p, "Q-0003", gate: "G-0002");

        var run = p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]);

        Assert.Equal(0, run.Code);
        var board = await ReadBoard(p);
        Assert.Contains("Q-0001", QuestsOf(board, "G-0002"), StringComparer.Ordinal);
        Assert.DoesNotContain(p.Events(), e => e.Type == "QuestMoved");
    }

    [Fact]
    public void Refuses_in_the_main_checkout()
    {
        using var p = new TempProject();
        Seed.Gate(p, "G-0001", "[Q-0001]");
        Seed.Gate(p, "G-0002", "[]");
        Seed.Quest(p, "Q-0001", gate: "G-0001");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        var before = Snapshot(p);

        var run = p.Run(["gate", "move-quest", "Q-0001", "--to", "G-0002"]);

        Assert.Equal(1, run.Code);
        Assert.Contains("worktree", run.Out + run.Err, StringComparison.Ordinal);
        Assert.Equal(before, Snapshot(p));
        Assert.DoesNotContain(p.Events(), e => e.Type == "QuestMoved");
    }

    private static string PlanTwo(string a, string b) =>
        $$"""{"gate":{"id":"G-0002","title":"Gate two","rank":"C","kind":"standard"},"quests":[{"id":"{{a}}","title":"Plain","kind":"main","rank":"D","scope":{"include":["src/web/**"],"exclude":[]},"estimate":{"session_pct":5,"weekly_pct":1},"dod":["a","b"]},{"id":"{{b}}","title":"Plain","kind":"main","rank":"D","scope":{"include":["src/web/**"],"exclude":[]},"estimate":{"session_pct":5,"weekly_pct":1},"dod":["a","b"]}]}""";

    private static List<JsonNode> Rpc(TempProject p, params string[] lines)
    {
        var o = new StringWriter();
        var env = new CliEnv(p.Root, p.Home, new StringReader(string.Join("\n", lines) + "\n"), o, new StringWriter(), p.Clock, p.Http, new GitHubTokenProvider(_ => "t", () => null));
        Assert.Equal(0, MonarchApp.Run(["mcp"], env));
        return [.. o.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!)];
    }

    private static string Call(int id, string tool, string args) =>
        $$$"""{"jsonrpc":"2.0","id":{{{id}}},"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{args}}}}}""";

    private static (bool IsError, string Text) Result(JsonNode r) =>
        (r["result"]!["isError"]?.GetValue<bool>() ?? false, r["result"]!["content"]![0]!["text"]!.GetValue<string>());

    private static string Plan(string questId) =>
        $$"""{"gate":{"id":"G-0002","title":"Gate two","rank":"C","kind":"standard"},"quests":[{"id":"{{questId}}","title":"Plain","kind":"main","rank":"D","scope":{"include":["src/web/**"],"exclude":[]},"estimate":{"session_pct":5,"weekly_pct":1},"dod":["a","b"]}]}""";

    private static async Task<(IReadOnlyList<Monarch.Application.Ports.BoardGate> Gates, IReadOnlyList<Monarch.Application.Ports.BoardQuest> Quests)> ReadBoard(TempProject p)
    {
        var board = new LocalFileBoard(p.Root);
        var ct = TestContext.Current.CancellationToken;
        return (await board.ListGatesAsync(ct), await board.ListQuestsAsync(ct));
    }

    private static string[] QuestsOf((IReadOnlyList<Monarch.Application.Ports.BoardGate> Gates, IReadOnlyList<Monarch.Application.Ports.BoardQuest> Quests) board, string gate) =>
        [.. board.Gates.Single(g => g.Id.Value == gate).Quests.Select(q => q.Value)];

    private static string QuestGate((IReadOnlyList<Monarch.Application.Ports.BoardGate> Gates, IReadOnlyList<Monarch.Application.Ports.BoardQuest> Quests) board, string quest) =>
        board.Quests.Single(q => q.Id.Value == quest).Gate.Value;

    private static string Body(string text)
    {
        const string mark = "\n---\n";
        var end = text.IndexOf(mark, StringComparison.Ordinal);
        return end < 0 ? text : text[(end + mark.Length)..];
    }

    private static Dictionary<string, string> Snapshot(TempProject p)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dir in new[] { "gates", "quests" })
        {
            var full = Path.Combine(p.Root, ".monarch", dir);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(full).Order(StringComparer.Ordinal))
            {
                map[file] = File.ReadAllText(file);
            }
        }

        return map;
    }

    private static string Actor(TempProject p, string type)
    {
        var dir = Path.Combine(p.Root, ".monarch", "ledger");
        foreach (var file in Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var doc = JsonDocument.Parse(line).RootElement;
                if (doc.GetProperty("type").GetString() == type)
                {
                    return doc.GetProperty("actor").GetString()!;
                }
            }
        }

        throw new InvalidOperationException($"no {type} event");
    }
}
