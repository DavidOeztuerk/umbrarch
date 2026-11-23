using Monarch.Domain;

namespace Monarch.Policy;

/// <summary>Entscheidung des Mana-Guards.</summary>
public enum ManaDecision
{
    /// <summary>Weiterarbeiten.</summary>
    Proceed,
    /// <summary>Recap schreiben und anhalten.</summary>
    RecapAndStop,
    /// <summary>Ohne Recap pausieren, Reset steht kurz bevor.</summary>
    PauseWithoutRecap,

    /// <summary>Ein Fenster, das nicht passt, ist laut Snapshot schon zurückgesetzt; der Stand wird erneuert (ADR-0020 §3).</summary>
    ResetPending,
}

/// <summary>Pacing-Modus des Wochenfensters.</summary>
public enum PacingMode
{
    /// <summary>Alle Quest-Typen.</summary>
    Normal,
    /// <summary>Nur Main und Emergency.</summary>
    MainAndEmergencyOnly,
}

/// <summary>Mana-Guard: entscheidet, ob ein weiterer Schritt noch bis zur Arbeitsgrenze passt (ADR-0012 Nachtrag Q-0189).</summary>
public static class ManaGuard
{
    /// <summary>Reset-Nähe, unter der ohne Recap pausiert wird.</summary>
    public static readonly TimeSpan SkipRecapIfResetWithin = TimeSpan.FromMinutes(10);

    /// <summary>Obergrenze für ResetPending nach dem Reset; gleich <c>ManaStore.StaleAfter</c> (ADR-0004, ADR-0020 §3).</summary>
    public static readonly TimeSpan ResetPendingFor = TimeSpan.FromMinutes(10);

    /// <summary>Abstand bis zur nächsten Prüfung bei ResetPending.</summary>
    public static readonly TimeSpan RecheckAfter = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Reihenfolge laut ADR-0020 §3: Proceed, wenn aktuell + Schritt in jedes vorhandene Fenster passt. Sind alle Fenster, die nicht passen, laut Snapshot
    /// schon zurückgesetzt: ResetPending bis <see cref="ResetPendingFor"/> nach dem spätesten dieser Resets, danach RecapAndStop. Liegt der Reset des
    /// maßgeblichen Fensters (5h, ohne 5h die Woche) in der Zukunft und weniger als 10 Minuten entfernt: PauseWithoutRecap. Sonst RecapAndStop.
    /// Veraltete Werte gehen über <see cref="ManaSnapshot.Effective5h"/> bzw. <see cref="ManaSnapshot.Effective7d"/> ein.
    /// </summary>
    public static ManaDecision Decide(ManaSnapshot now, double stepEstimate5h, double stepEstimate7d, ManaLimits limits, DateTimeOffset clock)
    {
        var fits = (!now.HasFiveHour || now.Effective5h + stepEstimate5h <= limits.FiveHourPct)
            && now.Effective7d + stepEstimate7d <= limits.SevenDayPct;
        if (fits) return ManaDecision.Proceed;
        if (ResetOfFailingWindows(now, stepEstimate5h, stepEstimate7d, limits, clock) is { } reset)
        {
            return clock - reset < ResetPendingFor ? ManaDecision.ResetPending : ManaDecision.RecapAndStop;
        }

        var until = (now.HasFiveHour ? now.FiveHourResetsAt : now.SevenDayResetsAt) - clock;
        return until > TimeSpan.Zero && until < SkipRecapIfResetWithin ? ManaDecision.PauseWithoutRecap : ManaDecision.RecapAndStop;
    }

    /// <summary>
    /// Spätester Reset unter den Fenstern, die nicht passen, wenn jedes davon laut Snapshot schon zurückgesetzt ist (<c>resets_at</c> ≤ jetzt); sonst null.
    /// Ein noch nicht zurückgesetztes Fenster über der Grenze ändert auch ein frischer Stand nicht.
    /// </summary>
    public static DateTimeOffset? ResetOfFailingWindows(ManaSnapshot now, double stepEstimate5h, double stepEstimate7d, ManaLimits limits, DateTimeOffset clock)
    {
        var fails5 = now.HasFiveHour && now.Effective5h + stepEstimate5h > limits.FiveHourPct;
        var fails7 = now.Effective7d + stepEstimate7d > limits.SevenDayPct;
        if (!fails5 && !fails7) return null;
        if ((fails5 && now.FiveHourResetsAt > clock) || (fails7 && now.SevenDayResetsAt > clock)) return null;
        var r5 = fails5 ? now.FiveHourResetsAt : DateTimeOffset.MinValue;
        var r7 = fails7 ? now.SevenDayResetsAt : DateTimeOffset.MinValue;
        return r5 > r7 ? r5 : r7;
    }

    /// <summary>
    /// Meldung bei ResetPending (ADR-0020 §3; der erste Satz ist der Wortlaut des Owners): nächste Prüfung in einer Minute, letzte bis
    /// <see cref="ResetPendingFor"/> nach dem Reset. Für jede andere Entscheidung null.
    /// </summary>
    public static string? PendingMessage(ManaDecision decision, ManaSnapshot now, double stepEstimate5h, double stepEstimate7d, ManaLimits limits, DateTimeOffset clock) =>
        decision == ManaDecision.ResetPending && ResetOfFailingWindows(now, stepEstimate5h, stepEstimate7d, limits, clock) is { } reset
            ? FormattableString.Invariant(
                $"Reset erfolgt, Stand wird erneuert. Erneut prüfen ab {(clock + RecheckAfter).UtcDateTime:HH:mm} UTC (in 1 Minute), letzte Prüfung bis {(reset + ResetPendingFor).UtcDateTime:HH:mm} UTC.")
            : null;

    /// <summary>MainAndEmergencyOnly, wenn der Wochenverbrauch mehr als <paramref name="tolerancePts"/> über dem verstrichenen Wochenanteil liegt.</summary>
    public static PacingMode Pacing(double weeklyUsedPct, DateTimeOffset weeklyResetsAt, DateTimeOffset clock, double tolerancePts = 15)
    {
        var elapsed = Math.Clamp(1 - ((weeklyResetsAt - clock).TotalDays / 7), 0, 1);
        return weeklyUsedPct > (elapsed * 100) + tolerancePts ? PacingMode.MainAndEmergencyOnly : PacingMode.Normal;
    }
}
