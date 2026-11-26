---
id: Q-0222
gate: G-0132
kind: main
rank: A
state: proposed
dod:
  - "Roter Test zuerst: Inline-Code liest ~/.claude/projects/x.jsonl -> erlaubt"
  - "Wächter: Inline-Code mit ~/.claude/settings.json, ~/.claude/plugins/x, .claude/settings.local.json, .monarch/ledger bleibt Deny"
  - "Keine Lockerung nach ADR-0014/0017 für Schreibwege"
scope:
  include: ["src/Monarch.Policy/**", "tests/Monarch.Policy.Tests/**"]
  exclude: []
estimate: { session_pct: 10, weekly_pct: 2, gold_usd: 0 }
boss: "Policy-Tests grün, Wächter Deny"
---
# Fehlalarm #104: python3-Inline nur bei echtem Schutzpfad verweigern
