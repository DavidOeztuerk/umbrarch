namespace Monarch.Cli.Tests;

/// <summary>Q-0184 (Gate 2): Fallenliste, Reihenfolge-Checkliste und Regel 7 in regeln/SKILL.md; Schutzregel-Befund in warden.md.</summary>
public class RegelnTextTests
{
    private static string PluginFile(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "plugin", "skills")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(dir);
        return Path.Combine([dir!, "plugin", .. parts]);
    }

    private static string Skill() => File.ReadAllText(PluginFile("skills", "regeln", "SKILL.md"));

    [Fact]
    public void Skill_lists_the_known_traps()
    {
        var text = Skill();
        Assert.Contains("Liste bekannter Fallen", text, StringComparison.Ordinal);
        Assert.Contains("~/.monarch/bin/monarch", text, StringComparison.Ordinal);
        Assert.Contains("nie über eine Variable", text, StringComparison.Ordinal);
        Assert.Contains("als Skriptdatei", text, StringComparison.Ordinal);
        Assert.Contains("Nie nach .monarch/", text, StringComparison.Ordinal);
        Assert.Contains("unquotierte", text, StringComparison.Ordinal);
        Assert.Contains("`bash`-Array", text, StringComparison.Ordinal);
        Assert.Contains("monarch recap", text, StringComparison.Ordinal);
        Assert.Contains("nie im Haupt-Checkout", text, StringComparison.Ordinal);
        Assert.Contains("project = Worktree", text, StringComparison.Ordinal);
        Assert.Contains("auf ready setzen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Skill_has_the_gate_checklist_in_the_required_order()
    {
        var all = Skill();
        Assert.Contains("Reihenfolge je Gate", all, StringComparison.Ordinal);
        var text = all[all.IndexOf("Reihenfolge je Gate", StringComparison.Ordinal)..];
        Assert.DoesNotContain("EnterWorktree", text[..text.IndexOf("gate_plan", StringComparison.Ordinal)], StringComparison.Ordinal);
        string[] steps = ["Owner legt die Gate-Worktree", "~/.monarch/bin/monarch gate open <G-xxxx> <slug>", "startet die Session dort", "gate carry", "gate_plan", "quest accept", "quest start", "TDD", "quest submit", "commit", "PR", "quest pr", "Warden", "verdict", "gate merge", "gate check", "evidence sync", "Owner führt im eigenen Terminal", "gate cleanup", "git worktree remove"];
        var positions = steps.Select(s => text.IndexOf(s, StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0, "Checklistenschritt fehlt"));
        for (var i = 1; i < positions.Length; i++)
        {
            Assert.True(positions[i] > positions[i - 1], $"Schritt '{steps[i]}' steht nicht nach '{steps[i - 1]}'");
        }
    }

    [Fact]
    public void Cleanup_after_the_merge_runs_in_the_owner_terminal()
    {
        var text = Skill();
        var start = text.IndexOf("**Aufräumen nach Merge und Clear", StringComparison.Ordinal);
        Assert.True(start >= 0, "Aufräum-Regel fehlt");
        var end = text.IndexOf("Nie `--force` vorschlagen", start, StringComparison.Ordinal);
        Assert.True(end > start, "Ende der Aufräum-Regel fehlt");
        var rule = text[start..end];
        Assert.Contains("im eigenen Terminal", rule, StringComparison.Ordinal);
        Assert.Contains("gate cleanup", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("ExitWorktree", rule, StringComparison.Ordinal);
        Assert.DoesNotContain("ExitWorktree", text[text.IndexOf("Reihenfolge je Gate", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void Local_tests_run_through_the_script_in_rules_and_knight()
    {
        Assert.Contains("scripts/test-local.sh", Skill(), StringComparison.Ordinal);
        Assert.Contains("scripts/test-local.sh", File.ReadAllText(PluginFile("agents", "knight.md")), StringComparison.Ordinal);
        var script = File.ReadAllText(Path.Combine(Path.GetDirectoryName(PluginFile("x"))!, "..", "scripts", "test-local.sh"));
        Assert.Contains("for name in Domain Policy Infrastructure Cli", script, StringComparison.Ordinal);
        Assert.Contains("--disable-build-servers -m:1 -nodeReuse:false", script, StringComparison.Ordinal);
        foreach (var filter in new[] { "-method", "-class", "--filter", "-trait", "-notrait" })
        {
            Assert.DoesNotContain(filter, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Knights_never_run_with_worktree_isolation()
    {
        foreach (var text in new[] { Skill(), File.ReadAllText(PluginFile("skills", "quest", "SKILL.md")) })
        {
            var line = text.Split('\n').FirstOrDefault(l => l.Contains("isolation: worktree", StringComparison.Ordinal));
            Assert.NotNull(line);
            Assert.Contains("Knight", line, StringComparison.Ordinal);
            Assert.Contains("nie", line, StringComparison.Ordinal);
            Assert.Contains("origin/HEAD", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Regel_7_separates_cosmetic_minor_findings_from_the_follow_up_issue()
    {
        var text = Skill();
        var start = text.IndexOf("\n7. ", StringComparison.Ordinal);
        Assert.True(start >= 0, "Regel 7 fehlt");
        var end = text.IndexOf("\n8. ", start + 1, StringComparison.Ordinal);
        var rule = end < 0 ? text[start..] : text[start..end];
        Assert.Contains("Kosmetische", rule, StringComparison.Ordinal);
        Assert.Contains("PR-Kommentar", rule, StringComparison.Ordinal);
        Assert.Contains("Nacharbeit G-", rule, StringComparison.Ordinal);
        Assert.Contains("Phase 1+", rule, StringComparison.Ordinal);
        Assert.Contains("Schutzlücke", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void Warden_treats_an_ineffective_protection_rule_as_major()
    {
        var text = File.ReadAllText(PluginFile("agents", "warden.md"));
        var line = text.Split('\n').FirstOrDefault(l => l.Contains("Schutzregel", StringComparison.Ordinal) && l.Contains("major", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("immer major", line, StringComparison.Ordinal);
    }
}
