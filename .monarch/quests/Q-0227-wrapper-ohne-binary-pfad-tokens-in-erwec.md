---
id: Q-0227
gate: G-0134
kind: main
rank: A
state: cleared
dod:
  - "Roter Test vorher in plugin/tests/wrapper-test.sh"
  - "Pfad-Token (absolut, -C, cd) in erwecktem Projekt -> ask"
  - "Ohne solches Token weiter fail-open"
  - "Keine Tests abgeschwächt"
scope:
  include: ["plugin/scripts/**", "plugin/tests/**"]
  exclude: []
estimate: { session_pct: 5, weekly_pct: 1, gold_usd: 0 }
boss: "Hook ohne Binary: Befehl mit Pfad-Token in erwecktes Projekt wird erfragt, ohne Token bleibt fail-open"
board: { pr: 182 }
---
# Wrapper ohne Binary: Pfad-Tokens in erwecktes Projekt fragen (#158)
