---
id: Q-0194
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen und Q-0193 fertig (gemeinsame Pfade in src/Monarch.Policy)"
  - "tools/shadow-costs.py liest alle Ledger und gibt je Shadow und Modell n, Median, p90 und Max aus (Filter laut ADR-0020); Test mit Fixture-Ledger"
  - "PreToolUse-Matcher in plugin/hooks/hooks.json enthält das Agent-Werkzeug; HookToolInput (Dtos.cs) liest subagent_type und model"
  - "Vor dem Start eines Shadows prüft der Hook aktuell + Kosten (Shadow, Modell) gegen die Grenze; ohne Messwert gilt ein konservativer Default laut ADR-0020; passt es nicht: Deny mit Stand, Kosten und Reset-Zeit"
  - "Im laufenden Shadow (AgentId gesetzt) verweigert der Hook ab der Grenze weitere Werkzeuge mit dem Hinweis, zu berichten und zu stoppen; der Main ohne AgentId ist davon nicht betroffen"
  - "Freshness: veralteter Snapshot (StaleAfter, State.cs:58) führt zum Verhalten laut ADR-0020; Tests mit altem und frischem Snapshot"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["plugin/hooks/**", "src/Monarch.Cli/HookRunner.cs", "src/Monarch.Cli/Dtos.cs", "src/Monarch.Cli/State.cs", "src/Monarch.Cli/Support.cs", "src/Monarch.Cli/SystemConfig.cs", "src/Monarch.Policy/**", "src/Monarch.Domain/Mana.cs", "tools/**", "tests/**"]
  exclude: []
estimate: { session_pct: 20, weekly_pct: 5, gold_usd: 0 }
boss: "Hook-Tests: Shadow-Start mit Kosten über der Resthöhe deny, darunter allow; Werkzeug im Shadow über der Grenze deny, im Main allow; dotnet test grün"
board: { pr: 163 }
---
# Mana-Bremse für Shadows: Prüfung vor dem Start, Stopp im laufenden Shadow (#161, #74)
