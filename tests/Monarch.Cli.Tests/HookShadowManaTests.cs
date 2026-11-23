using System.Globalization;
using System.Text.Json;
using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Cli.Tests;

/// <summary>Q-0194: Mana-Bremse für Shadows im Hook (ADR-0020 §2a und §2b).</summary>
public sealed class HookShadowManaTests
{
    private const string Pro = "mana:\n  plan: pro\n";
    private static readonly long Future = TempProject.Now.AddHours(2).ToUnixTimeSeconds();
    private static readonly long Past = TempProject.Now.AddMinutes(-30).ToUnixTimeSeconds();

    private static void WriteMana(TempProject p, double five, double seven, long fiveReset, TimeSpan? age = null)
    {
        var at = ManaStore.Stamp(p.Clock.UtcNow - (age ?? TimeSpan.Zero));
        var s = TempProject.Now.AddDays(3).ToUnixTimeSeconds();
        File.WriteAllText(Path.Combine(p.Home, "mana.json"), FormattableString.Invariant(
            $$$"""{"schema":1,"written_at":"{{{at}}}","rate_limits_at":"{{{at}}}","rate_limits":{"five_hour":{"used_percentage":{{{five}}},"resets_at":{{{fiveReset}}}},"seven_day":{"used_percentage":{{{seven}}},"resets_at":{{{s}}}}},"sessions":{"sess1":{"used_percentage":10,"written_at":"{{{at}}}","model":"claude-opus-4-7"}}""") + "}");
    }

    private static string AgentCall(string tool, string toolInput, string agentFields = "") =>
        $$"""{"session_id":"sess1","hook_event_name":"PreToolUse","permission_mode":"default"{{agentFields}},"tool_name":"{{tool}}","tool_input":{{toolInput}}}""";

    private static string Spawn(string type, string? model = null) =>
        AgentCall("Agent", model is null ? $$"""{"subagent_type":"{{type}}","prompt":"x"}""" : $$"""{"subagent_type":"{{type}}","model":"{{model}}","prompt":"x"}""");

    private const string InShadow = ""","agent_id":"a1","agent_type":"umbrarch:knight" """;

    private static (string? Decision, string? Reason) Decide(TempProject p, string json)
    {
        var r = p.Hook("pre-tool-use", json);
        Assert.Equal(0, r.Code);
        if (r.Out.Length == 0) return (null, null);
        var o = JsonDocument.Parse(r.Out).RootElement.GetProperty("hookSpecificOutput");
        return (o.GetProperty("permissionDecision").GetString(), o.GetProperty("permissionDecisionReason").GetString());
    }

    private static JsonElement Started(TempProject p) => Assert.Single(p.Events(), e => e.Type == "ShadowStarted").Payload;

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, ".monarch", "system.yaml")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return dir;
    }

    [Fact]
    public void Warden_start_over_the_limit_is_denied_and_logged()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 73, 50, Future);
        var (decision, reason) = Decide(p, Spawn("umbrarch:warden"));
        Assert.Equal("deny", decision);
        Assert.Contains("5h 73% + 18 (Grenze 90%, Reset 14:00 UTC)", reason, StringComparison.Ordinal);
        var ev = Assert.Single(p.Events(), e => e.Type == "PolicyDecided");
        Assert.Equal(PolicyRules.ManaShadowStart, ev.Payload.GetProperty("rule").GetString());
        var s = Started(p);
        Assert.Equal("warden", s.GetProperty("agent_type").GetString());
        Assert.Equal("opus", s.GetProperty("model_class").GetString());
        Assert.Equal(18, s.GetProperty("est_5h").GetDouble());
        Assert.Equal(3, s.GetProperty("est_7d").GetDouble());
        Assert.Equal(73, s.GetProperty("five_hour_used_pct").GetDouble());
        Assert.False(s.GetProperty("stale").GetBoolean());
        Assert.Equal("deny", s.GetProperty("decision").GetString());
        Assert.Equal(0, p.Run(["ledger", "verify"]).Code);
    }

    [Fact]
    public void Warden_start_that_fits_is_allowed_and_logged()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 72, 50, Future);
        Assert.Equal((null, null), Decide(p, Spawn("umbrarch:warden")));
        Assert.Equal("allow", Started(p).GetProperty("decision").GetString());
        Assert.DoesNotContain(p.Events(), e => e.Type == "PolicyDecided");
    }

    [Theory]
    [InlineData(78, null)]
    [InlineData(79, "deny")]
    public void Knight_uses_its_measured_cost(double five, string? expected)
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, five, 50, Future);
        Assert.Equal(expected, Decide(p, Spawn("umbrarch:knight")).Decision);
        Assert.Equal(12, Started(p).GetProperty("est_5h").GetDouble());
    }

    [Fact]
    public void Model_parameter_overrides_the_definition()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 78, 50, Future);
        Assert.Equal("deny", Decide(p, Spawn("umbrarch:knight", "opus")).Decision);
        Assert.Equal("opus", Started(p).GetProperty("model_class").GetString());
    }

    [Fact]
    public void Foreign_agent_uses_the_default_cost()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 73, 50, Future);
        Assert.Equal("deny", Decide(p, Spawn("general-purpose")).Decision);
        Assert.Equal(18, Started(p).GetProperty("est_5h").GetDouble());
    }

    [Fact]
    public void Non_string_fields_count_as_missing_and_raise_no_hook_error()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 73, 50, Future);
        var (decision, reason) = Decide(p, AgentCall("Task", """{"subagent_type":5,"model":{},"prompt":"x"}"""));
        Assert.Equal("deny", decision);
        Assert.DoesNotContain("Hook-Fehler", reason, StringComparison.Ordinal);
        Assert.Equal(PolicyRules.ManaShadowStart, Assert.Single(p.Events(), e => e.Type == "PolicyDecided").Payload.GetProperty("rule").GetString());
    }

    [Fact]
    public void Week_over_the_limit_denies_the_start()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 10, 94, Future);
        Assert.Equal("deny", Decide(p, Spawn("umbrarch:warden")).Decision);
    }

    [Fact]
    public void Without_mana_file_the_start_is_allowed_and_logged_without_usage()
    {
        using var p = new TempProject(systemYaml: Pro);
        Assert.Equal((null, null), Decide(p, Spawn("umbrarch:warden")));
        var s = Started(p);
        Assert.False(s.TryGetProperty("five_hour_used_pct", out _));
        Assert.Equal("allow", s.GetProperty("decision").GetString());
    }

    [Fact]
    public void Shadow_tool_at_the_limit_is_braked()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 90, 50, Future);
        var (decision, reason) = Decide(p, AgentCall("Read", """{"file_path":"src/a.cs"}""", InShadow));
        Assert.Equal("deny", decision);
        Assert.Equal("Mana-Grenze erreicht (5h 90 %, Grenze 90 %). Stoppe und melde deinen Stand als unvollständig an die Hauptsession.", reason);
        var ev = Assert.Single(p.Events(), e => e.Type == "PolicyDecided");
        Assert.Equal(PolicyRules.ManaBrake, ev.Payload.GetProperty("rule").GetString());
        Assert.Equal("a1", ev.Payload.GetProperty("agent_id").GetString());
        Assert.DoesNotContain(p.Events(), e => e.Type == "ShadowStarted");
    }

    [Fact]
    public void Shadow_tool_below_the_limit_is_allowed()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 89, 50, Future);
        Assert.Equal((null, null), Decide(p, AgentCall("Read", """{"file_path":"src/a.cs"}""", InShadow)));
    }

    [Fact]
    public void Main_tool_at_the_limit_is_not_braked()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 95, 50, Future);
        Assert.Equal((null, null), Decide(p, AgentCall("Read", """{"file_path":"src/a.cs"}""")));
    }

    [Fact]
    public void Old_snapshot_from_a_reset_window_does_not_brake_but_blocks_a_new_shadow()
    {
        using var p = new TempProject(systemYaml: Pro);
        WriteMana(p, 95, 50, Past, TimeSpan.FromMinutes(30));
        Assert.Equal((null, null), Decide(p, AgentCall("Read", """{"file_path":"src/a.cs"}""", InShadow)));

        using var q = new TempProject(systemYaml: Pro);
        WriteMana(q, 95, 50, Past, TimeSpan.FromMinutes(30));
        var (decision, reason) = Decide(q, Spawn("umbrarch:warden"));
        Assert.Equal("deny", decision);
        Assert.Contains("veraltet", reason, StringComparison.Ordinal);
        Assert.True(Started(q).GetProperty("stale").GetBoolean());
    }

    [Theory]
    [InlineData("Glob", """{"pattern":"**/*.cs"}""")]
    [InlineData("WebFetch", """{"url":"https://example.com","prompt":"x"}""")]
    [InlineData("WebSearch", """{"query":"x"}""")]
    [InlineData("Agent", """{"subagent_type":"umbrarch:scout","prompt":"x"}""")]
    [InlineData("Task", """{"subagent_type":"umbrarch:scout","prompt":"x"}""")]
    public void Newly_matched_tools_stay_allowed_without_mana_findings(string tool, string toolInput)
    {
        using var p = new TempProject(systemYaml: Pro);
        Assert.Equal((null, null), Decide(p, AgentCall(tool, toolInput)));
    }

    [Fact]
    public void Hooks_json_matcher_includes_shadow_and_work_tools()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "plugin", "hooks", "hooks.json")));
        var matcher = doc.RootElement.GetProperty("hooks").GetProperty("PreToolUse")[0].GetProperty("matcher").GetString()!.Split('|');
        foreach (var t in new[] { "Read", "Grep", "Edit", "Write", "MultiEdit", "NotebookEdit", "Bash", "mcp__.*", "Agent", "Task", "Glob", "WebFetch", "WebSearch" })
            Assert.Contains(t, matcher);
        Assert.DoesNotContain("SubagentHandback", matcher);
        Assert.DoesNotContain("TodoWrite", matcher);
    }

    [Fact]
    public void Known_agent_models_match_the_agent_definitions()
    {
        var dir = Path.Combine(RepoRoot(), "plugin", "agents");
        var fromFiles = new Dictionary<string, ModelClass>(StringComparer.Ordinal);
        foreach (var f in Directory.GetFiles(dir, "*.md"))
        {
            var line = File.ReadLines(f).First(l => l.TrimStart().StartsWith("model:", StringComparison.Ordinal));
            fromFiles[Path.GetFileNameWithoutExtension(f)] = ModelClasses.Of(line.Split(':', 2)[1].Trim());
        }

        Assert.Equal(fromFiles.OrderBy(k => k.Key), ShadowMana.KnownAgentModels.OrderBy(k => k.Key));
    }
}
