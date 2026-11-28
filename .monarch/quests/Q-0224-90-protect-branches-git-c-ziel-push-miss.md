---
id: Q-0224
gate: G-0133
kind: main
rank: A
state: in_review
dod:
  - "Test rot vorher: Fall aus #90 erlaubt; Waechter Deny fuer git -C <main> push, GIT_DIR, --git-dir, origin develop, HEAD:main"
  - "Nur -C-Ziele; GIT_DIR, --git-dir, --work-tree bleiben streng (alle Kandidaten)"
  - "Keine bestehende Pruefung abgeschwaecht"
scope:
  include: ["src/Monarch.Policy/ProtectBranchesRule.cs", "tests/Monarch.Policy.Tests/ProtectBranchesTests.cs"]
  exclude: []
estimate: { session_pct: 5, weekly_pct: 1, gold_usd: 0 }
---
# #90 protect_branches: git -C <ziel> push misst nur das -C-Ziel
