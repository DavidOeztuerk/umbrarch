---
id: Q-0198
gate: G-0130
kind: main
rank: C
state: proposed
dod:
  - "Voraussetzung: Q-0193 bis Q-0197 fertig (Skills beschreiben den gebauten Stand)"
  - "Regeln-Skill (plugin/skills/regeln/SKILL.md): Kommentar-Regel (keine Historie im Code), Warden-Ablauf (höchstens eine Delta-Runde; „gilt als freigegeben“ ersetzt kein Approve), nie --force vorschlagen"
  - "Gate- und Mana-Skill: gate move-quest, gate cleanup, Gate-Status merged und „Reset erfolgt“"
  - "Texttests (RegelnTextTests.cs oder neue Texttests) prüfen jede neue Regel wörtlich"
scope:
  include: ["plugin/skills/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 5, weekly_pct: 1, gold_usd: 0 }
boss: "Texttests grün mit den neuen Sätzen; dotnet test grün"
---
# Regeln-, Gate- und Mana-Skill: Kommentar-Regel, Warden-Ablauf, kein --force, neue Befehle
