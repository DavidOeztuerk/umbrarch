---
id: Q-0226
gate: G-0134
kind: main
rank: A
state: cleared
dod:
  - "Roter Test vorher für grep -f.env x, rg -f.env x, grep -r --include=.env.x PASS ."
  - "Gegenproben ohne Credential-Namen bleiben erlaubt"
  - "IsSecretToken in src/Monarch.Policy/ToolCallPolicy.cs erweitert, keine Tests abgeschwächt"
  - "scripts/test-local.sh grün"
scope:
  include: ["src/Monarch.Policy/**", "tests/**"]
  exclude: []
estimate: { session_pct: 5, weekly_pct: 1, gold_usd: 0 }
boss: "grep -f.env x, rg -f.env x und grep -r --include=.env.x PASS . werden verweigert; Gegenproben ohne Credential-Namen bleiben erlaubt"
board: { pr: 182 }
---
# protect_secrets: Werte direkt an Option und --include prüfen (#175)
