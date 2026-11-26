---
id: Q-0196
gate: G-0130
kind: main
rank: C
state: in_review
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen und Q-0195 fertig (McpTools.cs gemeinsam)"
  - "monarch gate move-quest <Q> --to <G> ändert in einem Schritt alte Gate-Datei (Quest entfernt), neue Gate-Datei (Quest ergänzt) und Quest-Datei (gate: G) und schreibt QuestMoved ins Ledger"
  - "Unbekannte Quest, unbekanntes Gate oder unzulässiger Quest-Zustand (laut ADR-0020): Abbruch ohne Teiländerung"
  - "gate_plan entfernt eine Quest, die es einem anderen Gate zuordnet, aus der Liste des alten Gates (McpTools.cs:330-332)"
  - "ledger verify ok nach dem Verschieben (Test)"
scope:
  include: ["src/Monarch.Cli/GateCommands.cs", "src/Monarch.Cli/MonarchApp.cs", "src/Monarch.Cli/McpTools.cs", "src/Monarch.Infrastructure.Boards/**", "tests/**"]
  exclude: []
estimate: { session_pct: 8, weekly_pct: 2, gold_usd: 0 }
boss: "CLI-Test im Temp-Projekt: nach move-quest alte Gate-Datei ohne, neue mit der Quest, Quest-Datei gate: G, QuestMoved im Ledger, ledger verify ok"
board: { pr: 163 }
---
# gate move-quest <Q> --to <G> mit QuestMoved; gate_plan räumt das alte Gate (#16, #81)
