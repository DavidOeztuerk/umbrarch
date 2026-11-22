---
id: Q-0195
gate: G-0130
kind: main
rank: A
state: proposed
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen und Q-0194 fertig (ManaPolicy.cs gemeinsam)"
  - "Ist resets_at des maßgeblichen Fensters vorbei, liefert ManaGuard.Decide (ManaPolicy.cs:37-44) einen neuen Fall statt RecapAndStop, begrenzt durch die Obergrenze aus ADR-0020; danach gilt das bisherige Verhalten"
  - "Alle Aufrufer melden „Reset erfolgt, Stand wird erneuert“: quest accept (QuestCommands.cs:67-89), ManaCommands.cs:240, :302, :460, StatusCommand.cs:45 und MCP mana_forecast"
  - "Weckzeit = Reset + 1 Minute (WakePlan.Slack, WakePlan.cs:22); geänderte Tests mit altem und neuem Wert und Grund im PR"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["src/Monarch.Policy/ManaPolicy.cs", "src/Monarch.Cli/ManaCommands.cs", "src/Monarch.Cli/QuestCommands.cs", "src/Monarch.Cli/StatusCommand.cs", "src/Monarch.Cli/McpTools.cs", "src/Monarch.Domain/WakePlan.cs", "src/Monarch.Cli/WakeCommands.cs", "tests/**"]
  exclude: []
estimate: { session_pct: 10, weekly_pct: 3, gold_usd: 0 }
boss: "Decide-Test mit resets_at in der Vergangenheit und vollem Fenster: neuer Fall statt RecapAndStop; WakePlan-Test Reset + 1 min; dotnet test grün"
---
# Mana: Fall „Reset erfolgt“ statt RecapAndStop, Weckruf Reset + 1 Minute
