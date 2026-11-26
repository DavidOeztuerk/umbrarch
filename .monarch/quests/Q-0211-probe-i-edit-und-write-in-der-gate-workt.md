---
id: Q-0211
gate: G-0130
kind: side
rank: A
state: in_review
dod:
  - "Owner-Probe i vom 26.11.2026: Edit und Write auf eine neue Datei in der Gate-Worktree gehen, ebenso ein Knight-Probelauf mit Write im eigenen Arbeitsverzeichnis"
  - "Probe-Dateien danach entfernt, keine Repo-Änderung außer .monarch"
scope:
  include: ["tmp-probe-i/**"]
  exclude: []
estimate: { session_pct: 1, weekly_pct: 0.2, gold_usd: 0 }
board: { pr: 163 }
---
# Probe i: Edit und Write in der Gate-Worktree gehen trotz Edit-Regeln (ohne Repo-Änderung)
