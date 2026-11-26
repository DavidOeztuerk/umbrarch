using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Monarch.Policy;

namespace Monarch.Cli.Tests;

/// <summary>Q-0195: pending message after a fresh reset (ADR-0020 §3).</summary>
public sealed class ManaResetPendingCliTests
{
    private static string Yaml(string five, string seven) =>
        $"mana:\n  plan: pro\n  limits:\n    five_hour: {{ {five} }}\n    seven_day: {{ {seven} }}\n";

    private static string Row(int seq, string type, string payload, DateTimeOffset at) =>
        FormattableString.Invariant($$"""{"seq":{{seq}},"at":"{{at.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}}","actor":"cli","type":"{{type}}","payload":{{payload}}}""");

    /// <summary>Seeds a ledger file (not the session file) with recaps (cost 5h 0) and two snapshots in one window: 5h +20, 7d +8.</summary>
    private static void SeedLedger(TempProject p, int recaps, double recapCost7d = 0, IEnumerable<string>? extraLines = null, double step7 = 8)
    {
        var t = TempProject.Now.AddHours(-6);
        var lines = new List<string>
        {
            Row(1, "ManaSnapshotTaken", """{"reason":"mana","known":true,"fiveHourUsedPct":10,"fiveHourResetsAt":1000,"sevenDayUsedPct":10,"sevenDayResetsAt":2000,"stale":false}""", t),
            Row(2, "ManaSnapshotTaken", FormattableString.Invariant($$"""{"reason":"mana","known":true,"fiveHourUsedPct":30,"fiveHourResetsAt":1000,"sevenDayUsedPct":{{10 + step7}},"sevenDayResetsAt":2000,"stale":false}"""), t.AddHours(1)),
        };
        for (var i = 0; i < recaps; i++)
        {
            lines.Add(Row(3 + i, "RecapWritten", FormattableString.Invariant($$"""{"cost5h":0,"cost7d":{{recapCost7d}},"path":"x"}"""), t.AddHours(2).AddMinutes(i)));
        }

        // Defective lines go in the middle of the seed file, never at the end of the session file (the store refuses to append there).
        lines.InsertRange(1, extraLines ?? []);
        var dir = Path.Combine(p.Root, ".monarch", "ledger");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "seed.jsonl"), lines, new UTF8Encoding(false));
    }

    [Fact]
    public void Reset_pending_for_equals_the_stale_window() =>
        Assert.Equal(ManaStore.StaleAfter, ManaGuard.ResetPendingFor);

    [Fact]
    public void Mana_estimate_prints_the_pending_message()
    {
        using var p = new TempProject();
        Seed.Mana(p, 95, 20, TempProject.Now.AddMinutes(-5));
        var r = p.Run(["mana", "--estimate", "5"]);
        Assert.Equal(0, r.Code);
        Assert.Contains("ResetPending", r.Out, StringComparison.Ordinal);
        Assert.Contains("Reset erfolgt, Stand wird erneuert. Erneut prüfen ab 12:01 UTC (in 1 Minute), letzte Prüfung bis 12:05 UTC.", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_text_and_json_carry_the_pending_message()
    {
        using var p = new TempProject();
        Seed.Mana(p, 95, 20, TempProject.Now.AddMinutes(-5));
        var text = p.Run(["status"]);
        Assert.Equal(0, text.Code);
        Assert.Contains("Reset erfolgt, Stand wird erneuert.", text.Out, StringComparison.Ordinal);
        var json = JsonDocument.Parse(p.Run(["status", "--json"]).Out).RootElement;
        Assert.Equal("ResetPending", json.GetProperty("guard").GetString());
        Assert.StartsWith("Reset erfolgt", json.GetProperty("guardMessage").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Quest_accept_refuses_with_the_pending_message()
    {
        using var p = new TempProject(systemYaml: Yaml("work_until_pct: 90, clamp: [90, 97]", "work_until_pct: 90, clamp: [90, 97]"));
        SeedLedger(p, 5);
        Seed.Quest(p, "Q-0003");
        var fiveReset = p.Clock.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var sevenReset = p.Clock.UtcNow.AddDays(3).ToUnixTimeSeconds();
        File.WriteAllText(Path.Combine(p.Home, "mana.json"), FormattableString.Invariant(
            $$$"""{"schema":1,"written_at":"{{{ManaStore.Stamp(p.Clock.UtcNow)}}}","rate_limits_at":"{{{ManaStore.Stamp(p.Clock.UtcNow.AddHours(-2))}}}","rate_limits":{"five_hour":{"used_percentage":95,"resets_at":{{{fiveReset}}}},"seven_day":{"used_percentage":10,"resets_at":{{{sevenReset}}}}},"sessions":{}}"""));
        var accept = p.Run(["quest", "accept", "Q-0003"]);
        Assert.Equal(1, accept.Code);
        var text = accept.Out + accept.Err;
        Assert.Contains("Reset erfolgt, Stand wird erneuert.", text, StringComparison.Ordinal);
        Assert.Contains("letzte Prüfung bis 12:05 UTC", text, StringComparison.Ordinal);
        Assert.DoesNotContain("interaktive Session", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Mana_forecast_carries_the_pending_message()
    {
        using var w = new McpWorld();
        var project = w.Proj("a");
        var five = TempProject.Now.AddMinutes(-5).ToUnixTimeSeconds();
        var seven = TempProject.Now.AddDays(3).ToUnixTimeSeconds();
        var now = TempProject.Now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        File.WriteAllText(Path.Combine(w.Home, "mana.json"),
            FormattableString.Invariant($$$"""{"schema":1,"written_at":"{{{now}}}","rate_limits_at":"{{{now}}}","rate_limits":{"five_hour":{"used_percentage":95,"resets_at":{{{five}}}},"seven_day":{"used_percentage":20,"resets_at":{{{seven}}}}}""") + "}");
        var r = w.One(w.Elsewhere, [project], "mana_forecast", McpWorld.With(project, """"" "estimates":[{"session_pct":5,"weekly_pct":1}] """""));
        Assert.False(r.IsError, r.Text);
        var body = JsonNode.Parse(McpWorld.Body(r.Text))!;
        Assert.Equal("ResetPending", body["decision"]!.GetValue<string>());
        Assert.StartsWith("Reset erfolgt", body["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Hook_denies_a_shadow_start_after_a_fresh_reset_with_the_pending_message()
    {
        using var p = new TempProject();
        Seed.Mana(p, 95, 20, TempProject.Now.AddMinutes(-5));
        var r = p.Hook("pre-tool-use", """{"session_id":"sess1","hook_event_name":"PreToolUse","permission_mode":"default","tool_name":"Agent","tool_input":{"subagent_type":"umbrarch:warden","prompt":"x"}}""");
        Assert.Equal(0, r.Code);
        var o = JsonDocument.Parse(r.Out).RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("deny", o.GetProperty("permissionDecision").GetString());
        Assert.StartsWith("Reset erfolgt, Stand wird erneuert.", o.GetProperty("permissionDecisionReason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Rest_after_a_passed_reset_wakes_one_minute_from_now()
    {
        using var p = new TempProject();
        Seed.Mana(p, 95, 20, TempProject.Now.AddMinutes(-5));
        var r = p.Run(["rest"]);
        Assert.Equal(0, r.Code);
        Assert.Contains("Wake at 2026-10-27T12:01", r.Out, StringComparison.Ordinal);
        Assert.Contains("Reset erfolgt, Stand wird erneuert.", r.Out, StringComparison.Ordinal);
    }
}
