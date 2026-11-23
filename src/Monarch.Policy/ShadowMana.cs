using Monarch.Domain;

namespace Monarch.Policy;

/// <summary>Mana-Bremse für Shadows (ADR-0020 §2): Prüfung vor dem Start und Stopp im laufenden Shadow. Rein, ohne Datei- und Uhrzugriff.</summary>
public static class ShadowMana
{
    private const string BrakeWay = "Stoppe und melde deinen Stand als unvollständig an die Hauptsession.";

    /// <summary>Konservativer Default (5h, 7d) für jede Kombination ohne eigenen Messwert: der höchste belegte p90.</summary>
    public static readonly (double FiveHour, double SevenDay) DefaultCost = (18, 3);

    // p90 aus tools/shadow-costs.py, Lauf vom 23.11.2026 (ADR-0020 §2); ein eigener Wert erst ab n >= 5.
    private static readonly Dictionary<(string Agent, ModelClass Model), (double FiveHour, double SevenDay)> Measured = new()
    {
        [("warden", ModelClass.Opus)] = (18, 3),
        [("knight", ModelClass.Sonnet)] = (12, 2),
    };

    // Modell laut plugin/agents/*.md; HookShadowManaTests gleicht das ab.
    private static readonly Dictionary<string, ModelClass> AgentModels = new(StringComparer.Ordinal)
    {
        ["warden"] = ModelClass.Opus,
        ["kritiker"] = ModelClass.Opus,
        ["strategist"] = ModelClass.Opus,
        ["knight"] = ModelClass.Sonnet,
        ["scout"] = ModelClass.Sonnet,
    };

    /// <summary>Modelle der Agenten laut Definition.</summary>
    public static IReadOnlyDictionary<string, ModelClass> KnownAgentModels => AgentModels;

    /// <summary>Werkzeuge, mit denen ein Shadow startet.</summary>
    public static bool IsShadowStart(string? toolName) => toolName is "Agent" or "Task";

    /// <summary>Name des Shadows: der Teil von <c>subagent_type</c> nach dem letzten <c>:</c>; leer, wenn er fehlt.</summary>
    public static string AgentName(string? subagentType) =>
        string.IsNullOrWhiteSpace(subagentType) ? string.Empty : subagentType[(subagentType.LastIndexOf(':') + 1)..].Trim();

    /// <summary>
    /// Modellklasse des Shadows: <c>model</c> aus dem Aufruf (Familie aus dem Namen, <c>inherit</c> zählt als fehlend), sonst die Agent-Definition,
    /// sonst die Modellklasse der Session.
    /// </summary>
    public static ModelClass ModelClassOf(string? model, string agentName, ModelClass sessionClass)
    {
        if (!string.IsNullOrWhiteSpace(model) && !string.Equals(model.Trim(), "inherit", StringComparison.OrdinalIgnoreCase))
        {
            var m = model.ToLowerInvariant();
            return m.Contains("sonnet", StringComparison.Ordinal) ? ModelClass.Sonnet
                : m.Contains("haiku", StringComparison.Ordinal) ? ModelClass.Haiku
                : ModelClass.Opus;
        }

        return AgentModels.TryGetValue(agentName, out var known) ? known : sessionClass;
    }

    /// <summary>Erwartete Kosten (5h, 7d) eines Shadow-Laufs; ohne eigenen Messwert <see cref="DefaultCost"/>.</summary>
    public static (double FiveHour, double SevenDay) Cost(string agentName, ModelClass model) =>
        Measured.TryGetValue((agentName, model), out var c) ? c : DefaultCost;

    /// <summary>Prüfung vor dem Start: Deny mit Regel mana_shadow_start, wenn <see cref="ManaGuard.Decide"/> nicht Proceed ergibt. Ohne Snapshot kein Deny.</summary>
    public static PolicyDecision Start(ManaSnapshot? snapshot, string agentName, ModelClass model, ManaLimits limits, DateTimeOffset now)
    {
        if (snapshot is null) return PolicyDecision.Allow;
        var (c5, c7) = Cost(agentName, model);
        var decision = ManaGuard.Decide(snapshot, c5, c7, limits, now);
        if (decision == ManaDecision.Proceed) return PolicyDecision.Allow;
        var name = agentName.Length == 0 ? "unbekannt" : agentName;
        var five = snapshot.HasFiveHour
            ? FormattableString.Invariant($"5h {snapshot.Effective5h:0}% + {c5:0.#} (Grenze {limits.FiveHourPct:0.#}%, Reset {snapshot.FiveHourResetsAt.UtcDateTime:HH:mm} UTC)")
            : "5h: nicht vorhanden";
        var reset = snapshot.HasFiveHour ? snapshot.FiveHourResetsAt : snapshot.SevenDayResetsAt;
        // Ein Wert von vor dem Reset zeigt den alten Verbrauch; erst ein frischer Stand entscheidet.
        var stale = snapshot.IsStale && reset <= now
            ? " Der Stand ist veraltet: das Fenster ist schon zurückgesetzt, der Wert stammt von davor."
            : string.Empty;
        return PolicyDecision.Deny(PolicyRules.ManaShadowStart, FormattableString.Invariant(
            $"Mana reicht nicht für den Shadow {name} ({model}, {decision}): {five}; 7d {snapshot.Effective7d:0}% + {c7:0.#} (Grenze {limits.SevenDayPct:0.#}%, Reset {snapshot.SevenDayResetsAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC).{stale} Shadow nicht starten."));
    }

    /// <summary>
    /// Bremse im laufenden Shadow (nicht leere <paramref name="agentId"/>): Deny mit Regel mana_brake, wenn in einem noch nicht zurückgesetzten Fenster
    /// der Stand die Grenze erreicht. Ohne Snapshot kein Deny.
    /// </summary>
    public static PolicyDecision Brake(string? agentId, ManaSnapshot? snapshot, ManaLimits limits, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(agentId) || snapshot is null) return PolicyDecision.Allow;
        if (snapshot.HasFiveHour && snapshot.FiveHourResetsAt > now && snapshot.Effective5h >= limits.FiveHourPct)
        {
            return PolicyDecision.Deny(PolicyRules.ManaBrake,
                FormattableString.Invariant($"Mana-Grenze erreicht (5h {snapshot.Effective5h:0} %, Grenze {limits.FiveHourPct:0.#} %). {BrakeWay}"));
        }

        if (snapshot.SevenDayResetsAt > now && snapshot.Effective7d >= limits.SevenDayPct)
        {
            return PolicyDecision.Deny(PolicyRules.ManaBrake,
                FormattableString.Invariant($"Mana-Grenze erreicht (7d {snapshot.Effective7d:0} %, Grenze {limits.SevenDayPct:0.#} %). {BrakeWay}"));
        }

        return PolicyDecision.Allow;
    }
}
