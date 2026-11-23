using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>Q-0195: Fall „Reset erfolgt“ in ManaGuard.Decide (ADR-0020 §3).</summary>
public sealed class ManaResetPendingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly ManaLimits Pro = ManaLimits.ForPlan(PlanKind.Pro);

    private static ManaSnapshot Snap(double five, double seven, TimeSpan fiveResetIn, TimeSpan? sevenResetIn = null) =>
        new(five, Now + fiveResetIn, seven, Now + (sevenResetIn ?? TimeSpan.FromDays(3)));

    [Fact] public void Boss_five_hour_reset_5_min_ago_and_full_window_is_reset_pending() =>
        Assert.Equal(ManaDecision.ResetPending, ManaGuard.Decide(Snap(95, 20, TimeSpan.FromMinutes(-5)), 5, 1, Pro, Now));

    [Fact] public void Boss_five_hour_reset_11_min_ago_and_full_window_recaps() =>
        Assert.Equal(ManaDecision.RecapAndStop, ManaGuard.Decide(Snap(95, 20, TimeSpan.FromMinutes(-11)), 5, 1, Pro, Now));

    [Fact] public void Exactly_10_min_after_the_reset_recaps() =>
        Assert.Equal(ManaDecision.RecapAndStop, ManaGuard.Decide(Snap(95, 20, TimeSpan.FromMinutes(-10)), 5, 1, Pro, Now));

    [Fact] public void Reset_right_now_is_reset_pending() =>
        Assert.Equal(ManaDecision.ResetPending, ManaGuard.Decide(Snap(95, 20, TimeSpan.Zero), 5, 1, Pro, Now));

    [Fact] public void Boss_deciding_window_reset_and_fitting_while_another_does_not_fit_recaps() =>
        Assert.Equal(ManaDecision.RecapAndStop, ManaGuard.Decide(Snap(10, 99, TimeSpan.FromMinutes(-30)), 5, 1, Pro, Now));

    [Fact] public void Failing_window_reset_but_another_failing_window_not_reset_recaps() =>
        Assert.Equal(ManaDecision.RecapAndStop, ManaGuard.Decide(Snap(95, 99, TimeSpan.FromMinutes(-5)), 5, 1, Pro, Now));

    [Fact] public void Both_failing_windows_reset_counts_the_latest_reset() =>
        Assert.Equal(ManaDecision.ResetPending, ManaGuard.Decide(Snap(95, 99, TimeSpan.FromMinutes(-5), TimeSpan.FromMinutes(-60)), 5, 1, Pro, Now));

    [Fact] public void Future_reset_within_10_min_still_pauses() =>
        Assert.Equal(ManaDecision.PauseWithoutRecap, ManaGuard.Decide(Snap(95, 20, TimeSpan.FromMinutes(5)), 5, 1, Pro, Now));

    [Fact]
    public void Without_five_hour_window_a_reset_week_is_reset_pending()
    {
        var noFive = new ManaSnapshot(0, default, 97, Now.AddMinutes(-3), HasFiveHour: false);
        Assert.Equal(ManaDecision.ResetPending, ManaGuard.Decide(noFive, 0, 1.1, Pro, Now));
    }

    [Fact]
    public void Pending_message_names_the_next_and_the_last_check()
    {
        var s = Snap(95, 20, TimeSpan.FromMinutes(-5));
        var d = ManaGuard.Decide(s, 5, 1, Pro, Now);
        Assert.Equal("Reset erfolgt, Stand wird erneuert. Erneut prüfen ab 12:01 UTC (in 1 Minute), letzte Prüfung bis 12:05 UTC.",
            ManaGuard.PendingMessage(d, s, 5, 1, Pro, Now));
    }

    [Theory]
    [InlineData(70, 120)]
    [InlineData(95, 120)]
    [InlineData(95, 5)]
    [InlineData(95, -11)]
    public void Pending_message_is_null_for_every_other_decision(double five, int resetMin)
    {
        var s = Snap(five, 20, TimeSpan.FromMinutes(resetMin));
        Assert.Null(ManaGuard.PendingMessage(ManaGuard.Decide(s, 5, 1, Pro, Now), s, 5, 1, Pro, Now));
    }

    [Fact]
    public void Shadow_start_after_a_fresh_reset_names_the_pending_check()
    {
        var d = ShadowMana.Start(Snap(95, 20, TimeSpan.FromMinutes(-5)), "warden", ModelClass.Opus, Pro, Now);
        Assert.Equal(PolicyRules.ManaShadowStart, d.Rule);
        Assert.StartsWith("Reset erfolgt, Stand wird erneuert. Erneut prüfen ab 12:01 UTC", d.Reason, StringComparison.Ordinal);
    }
}
