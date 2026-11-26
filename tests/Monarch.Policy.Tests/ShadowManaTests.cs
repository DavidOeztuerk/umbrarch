using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>Q-0194: Mana-Bremse für Shadows (ADR-0020 §2), reine Entscheidungen.</summary>
public sealed class ShadowManaTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly ManaLimits Opus = new(90, 96);

    private static ManaSnapshot Snap(double five, double seven = 50, int fiveResetMin = 120, bool stale = false) =>
        new(five, Now.AddMinutes(fiveResetMin), seven, Now.AddDays(3), stale);

    [Theory]
    [InlineData("warden", ModelClass.Opus, 18, 3)]
    [InlineData("knight", ModelClass.Sonnet, 12, 2)]
    [InlineData("knight", ModelClass.Opus, 18, 3)]
    [InlineData("warden", ModelClass.Sonnet, 18, 3)]
    [InlineData("general-purpose", ModelClass.Opus, 18, 3)]
    [InlineData("", ModelClass.Haiku, 18, 3)]
    public void Cost_uses_the_measured_p90_or_the_default(string agent, ModelClass model, double five, double seven) =>
        Assert.Equal((five, seven), ShadowMana.Cost(agent, model));

    [Theory]
    [InlineData("umbrarch:warden", "warden")]
    [InlineData("knight", "knight")]
    [InlineData("a:b:scout", "scout")]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    public void Agent_name_is_the_part_after_the_last_colon(string? type, string expected) =>
        Assert.Equal(expected, ShadowMana.AgentName(type));

    [Theory]
    [InlineData("sonnet", "warden", ModelClass.Opus, ModelClass.Sonnet)]
    [InlineData("claude-haiku-5-5", "warden", ModelClass.Opus, ModelClass.Haiku)]
    [InlineData("opus", "knight", ModelClass.Sonnet, ModelClass.Opus)]
    [InlineData("claude-fable-5-2", "knight", ModelClass.Sonnet, ModelClass.Opus)]
    [InlineData("inherit", "knight", ModelClass.Opus, ModelClass.Sonnet)]
    [InlineData(null, "warden", ModelClass.Sonnet, ModelClass.Opus)]
    [InlineData(null, "general-purpose", ModelClass.Sonnet, ModelClass.Sonnet)]
    [InlineData(null, "", ModelClass.Opus, ModelClass.Opus)]
    public void Model_class_comes_from_the_call_then_the_definition_then_the_session(string? model, string agent, ModelClass session, ModelClass expected) =>
        Assert.Equal(expected, ShadowMana.ModelClassOf(model, agent, session));

    [Fact]
    public void Start_that_fits_exactly_is_allowed() =>
        Assert.Equal(PolicyDecisionKind.Allow, ShadowMana.Start(Snap(72), "warden", ModelClass.Opus, Opus, Now).Kind);

    [Fact]
    public void Start_over_the_limit_is_denied_with_state_cost_and_reset()
    {
        var d = ShadowMana.Start(Snap(73), "warden", ModelClass.Opus, Opus, Now);
        Assert.Equal(PolicyDecisionKind.Deny, d.Kind);
        Assert.Equal(PolicyRules.ManaShadowStart, d.Rule);
        Assert.Contains("5h 73% + 18 (Grenze 90%, Reset 14:00 UTC)", d.Reason, StringComparison.Ordinal);
        Assert.Contains("7d 50% + 3 (Grenze 96%", d.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("veraltet", d.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_over_the_week_limit_is_denied() =>
        Assert.Equal(PolicyRules.ManaShadowStart, ShadowMana.Start(Snap(10, seven: 94), "warden", ModelClass.Opus, Opus, Now).Rule);

    [Fact]
    public void Start_without_snapshot_is_allowed() =>
        Assert.Equal(PolicyDecisionKind.Allow, ShadowMana.Start(null, "warden", ModelClass.Opus, Opus, Now).Kind);

    [Fact]
    public void Start_with_a_stale_value_from_before_the_reset_says_so()
    {
        var d = ShadowMana.Start(Snap(95, fiveResetMin: -30, stale: true), "warden", ModelClass.Opus, Opus, Now);
        Assert.Equal(PolicyRules.ManaShadowStart, d.Rule);
        Assert.Contains("veraltet", d.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Brake_at_the_five_hour_limit_denies_with_the_fixed_text()
    {
        var d = ShadowMana.Brake("a1", Snap(90), Opus, Now);
        Assert.Equal(PolicyRules.ManaBrake, d.Rule);
        Assert.Equal("Mana-Grenze erreicht (5h 90 %, Grenze 90 %). Stoppe und melde deinen Stand als unvollständig an die Hauptsession.", d.Reason);
    }

    [Fact]
    public void Brake_at_the_week_limit_denies() =>
        Assert.Equal("Mana-Grenze erreicht (7d 96 %, Grenze 96 %). Stoppe und melde deinen Stand als unvollständig an die Hauptsession.",
            ShadowMana.Brake("a1", Snap(10, seven: 96), Opus, Now).Reason);

    [Theory]
    [InlineData(null, 95, 120)]
    [InlineData("  ", 95, 120)]
    [InlineData("a1", 89, 120)]
    [InlineData("a1", 95, -1)]
    [InlineData("a1", 95, 0)]
    public void Brake_allows_main_below_limit_and_a_window_that_reset(string? agentId, double five, int resetMin) =>
        Assert.Equal(PolicyDecisionKind.Allow, ShadowMana.Brake(agentId, Snap(five, fiveResetMin: resetMin), Opus, Now).Kind);

    [Fact]
    public void Brake_without_snapshot_is_allowed() =>
        Assert.Equal(PolicyDecisionKind.Allow, ShadowMana.Brake("a1", null, Opus, Now).Kind);
}
