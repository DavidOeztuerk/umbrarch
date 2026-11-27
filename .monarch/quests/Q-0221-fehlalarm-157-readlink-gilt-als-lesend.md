---
id: Q-0221
gate: G-0132
kind: main
rank: A
state: in_review
dod:
  - "Roter Test zuerst: readlink ~/.monarch/bin/monarch erlaubt"
  - "Wächter: readlink mit Umleitung in Schutzpfad bleibt Deny"
scope:
  include: ["src/Monarch.Policy/**", "tests/Monarch.Policy.Tests/**"]
  exclude: []
estimate: { session_pct: 5, weekly_pct: 1, gold_usd: 0 }
boss: "Policy-Test grün"
---
# Fehlalarm #157: readlink gilt als lesend
