---
id: Q-0223
gate: G-0133
kind: main
rank: C
state: cleared
dod:
  - "Ursache belegt (Frist 500 ms ueber die Uhr; Task.Delay(100) bei parallelen Requests)"
  - "Frist ueber den Fake steuerbar; kein Schwellwert gelockert"
  - "scripts/test-local.sh gruen bis auf den bekannten Real_ps-Test"
scope:
  include: ["tests/Monarch.Cli.Tests/ForgeClearViewTests.cs"]
  exclude: []
estimate: { session_pct: 4, weekly_pct: 1, gold_usd: 0 }
board: { pr: 181 }
---
# #171 ForgeClearViewTests: Deadline- und Parallel-Test ohne Uhrabhaengigkeit
