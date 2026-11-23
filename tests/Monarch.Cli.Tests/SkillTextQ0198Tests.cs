namespace Monarch.Cli.Tests;

/// <summary>Q-0198: Skills beschreiben den gebauten Stand von G-0130 (ADR-0020); jede neue Regel wörtlich.</summary>
public class SkillTextQ0198Tests
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

    private static string Read(string skill) => File.ReadAllText(PluginFile("skills", skill, "SKILL.md"));

    [Fact]
    public void Regeln_has_the_comment_rule()
    {
        var t = Read("regeln");
        Assert.Contains("**Kommentare im Code:** keine Historie (kein „früher“, „neu seit“, „vorher war“), höchstens zwei Zeilen und nur das Warum.", t, StringComparison.Ordinal);
    }

    [Fact]
    public void Regeln_has_the_warden_flow()
    {
        var t = Read("regeln");
        Assert.Contains("**„Gilt als freigegeben“ ersetzt kein approve.**", t, StringComparison.Ordinal);
        Assert.Contains("Mehr als eine Delta-Runde gibt es nicht", t, StringComparison.Ordinal);
    }

    [Fact]
    public void Regeln_never_suggests_force_and_uses_the_new_commands()
    {
        var t = Read("regeln");
        Assert.Contains("**Nie `--force` vorschlagen**", t, StringComparison.Ordinal);
        Assert.Contains("`~/.monarch/bin/monarch gate cleanup <G-xxxx>`", t, StringComparison.Ordinal);
        Assert.Contains("`~/.monarch/bin/monarch gate carry`", t, StringComparison.Ordinal);
        Assert.Contains("`~/.monarch/bin/monarch gate move-quest <Q-xxxx> --to <G-xxxx>`", t, StringComparison.Ordinal);
        Assert.Contains("(`protect_main_checkout`)", t, StringComparison.Ordinal);
        Assert.DoesNotContain("Bis das Aufräumen automatisiert ist", t, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_skill_names_merged_cleanup_carry_and_move_quest()
    {
        var t = Read("gate");
        Assert.Contains("steht das Gate auf `state: merged`", t, StringComparison.Ordinal);
        Assert.Contains("`~/.monarch/bin/monarch gate cleanup <G-xxxx>`", t, StringComparison.Ordinal);
        Assert.Contains("`~/.monarch/bin/monarch gate carry`", t, StringComparison.Ordinal);
        Assert.Contains("`~/.monarch/bin/monarch gate move-quest <Q-xxxx> --to <G-xxxx>`", t, StringComparison.Ordinal);
        Assert.Contains("Nie `--force` vorschlagen.", t, StringComparison.Ordinal);
    }

    [Fact]
    public void Mana_skill_names_reset_pending_the_shadow_brake_and_the_wake_time()
    {
        var t = Read("mana");
        Assert.Contains("„Reset erfolgt, Stand wird erneuert“ (`ResetPending`)", t, StringComparison.Ordinal);
        Assert.Contains("Kein Hintergrund-Prozess zum Warten.", t, StringComparison.Ordinal);
        Assert.Contains("(`mana_shadow_start`)", t, StringComparison.Ordinal);
        Assert.Contains("(`mana_brake`)", t, StringComparison.Ordinal);
        Assert.Contains("Reset + 1 Minute", t, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("awaken")]
    [InlineData("gate")]
    [InlineData("mana")]
    [InlineData("quest")]
    [InlineData("recap")]
    [InlineData("rest")]
    [InlineData("status")]
    public void Ground_rules_name_the_reset_pending_check(string skill) =>
        Assert.Contains("Meldet der Guard „Reset erfolgt, Stand wird erneuert“: nach 1 Minute erneut pruefen, bis zur letzten Pruefzeit der Meldung.", Read(skill), StringComparison.Ordinal);

    [Fact]
    public void Rest_skill_names_the_wake_time_reset_plus_one_minute() =>
        Assert.Contains("Die Weckzeit ist Reset + 1 Minute", Read("rest"), StringComparison.Ordinal);
}
