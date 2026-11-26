---
id: Q-0220
gate: G-0132
kind: main
rank: A
state: proposed
dod:
  - "Erst nach Q-0219 Probe grün"
  - "Rote Tests zuerst: Fälle aus #66 und #169 erlaubt; Wächter grep foo .env, grep -f .env x, grep foo x .env, jq . .env, cat ~/.netrc verweigert"
  - "Keine andere Regel gelockert, Hook bleibt Frühwarnung"
scope:
  include: ["src/Monarch.Policy/**", "tests/Monarch.Policy.Tests/**"]
  exclude: []
estimate: { session_pct: 15, weekly_pct: 3, gold_usd: 0 }
boss: "Policy-Tests grün, alle Wächter weiter Deny"
---
# Fehlalarme #66 und #169: Suchmuster und jq-Ausdruck sind keine Pfade (erst nach aktiver Sperre)
