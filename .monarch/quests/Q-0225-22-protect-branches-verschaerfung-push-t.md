---
id: Q-0225
gate: G-0133
kind: main
rank: A
state: cleared
dod:
  - "Test rot vorher und gruen danach"
  - "Fehlalarm gh pr comment --body merge NICHT gebaut (Kommentar: Lockerung bei Bash-Text, ADR-0022 §5, Milestone Phase 1+)"
scope:
  include: ["src/Monarch.Policy/ProtectBranchesRule.cs", "tests/Monarch.Policy.Tests/ProtectBranchesTests.cs"]
  exclude: []
estimate: { session_pct: 4, weekly_pct: 1, gold_usd: 0 }
board: { pr: 181 }
---
# #22 protect_branches: Verschaerfung push --tags auf geschuetzte Branches
