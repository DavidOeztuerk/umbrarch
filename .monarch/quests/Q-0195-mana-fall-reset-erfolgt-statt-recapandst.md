---
id: Q-0195
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen und Q-0194 fertig (ManaPolicy.cs gemeinsam)"
  - "ManaGuard.Decide in der Reihenfolge aus ADR-0020 Abschnitt 3: Proceed; zurückgesetztes Fenster, das nicht passt, unter 10 Minuten ResetPending, danach RecapAndStop mit Hinweis Stand veraltet; Reset des maßgeblichen Fensters in der Zukunft und unter 10 Minuten (0 < resets_at - jetzt < 10 Minuten) PauseWithoutRecap; sonst RecapAndStop"
  - "Alle Aufrufer melden Reset erfolgt, Stand wird erneuert mit nächster und letzter Prüfzeit: quest accept, mana --estimate, mana --json, status, MCP mana_forecast und der Hook vor einem Shadow-Start"
  - "Weckzeit = Reset + 1 Minute (WakePlan.Slack); liegt resets_at schon zurück, jetzt + 1 Minute; geänderte Tests mit altem und neuem Wert und Grund im PR"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["src/Monarch.Policy/ManaPolicy.cs", "src/Monarch.Cli/ManaCommands.cs", "src/Monarch.Cli/QuestCommands.cs", "src/Monarch.Cli/StatusCommand.cs", "src/Monarch.Cli/McpTools.cs", "src/Monarch.Cli/HookRunner.cs", "src/Monarch.Domain/WakePlan.cs", "src/Monarch.Cli/WakeCommands.cs", "src/Monarch.Cli/RestCommand.cs", "tests/**"]
  exclude: []
estimate: { session_pct: 10, weekly_pct: 3, gold_usd: 0 }
boss: "Decide-Tests: resets_at vor 5 Minuten und volles Fenster ergibt ResetPending, vor 11 Minuten RecapAndStop; maßgebliches Fenster schon zurückgesetzt und passend, ein anderes passt nicht: RecapAndStop; WakePlan-Test Reset + 1 min; dotnet test grün"
board: { pr: 163 }
---
# Mana: Fall „Reset erfolgt“ statt RecapAndStop, Weckruf Reset + 1 Minute
