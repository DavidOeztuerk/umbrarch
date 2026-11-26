---
id: Q-0217
gate: G-0131
kind: main
rank: B
state: cleared
dod:
  - "gate open nach ADR-0021 §2: verweigert in einer Claude-Code-Session vor jedem git-Aufruf; prüft G (^G-\\d{4,}$) und slug (^[a-z0-9]+(-[a-z0-9]+)*$, höchstens 60 Zeichen); Haupt-Checkout aus git worktree list; git -C <main> fetch origin; git -C <main> worktree add --no-track <main>/.claude/worktrees/<G> -b monarch/<G>-<slug> origin/<integration_branch>; stdout nur der absolute Pfad, git-Ausgabe nach stderr; git-Fehler mit Exit 1 ohne Pfad; kein Ledger-Eintrag"
  - "Tests vorher rot, danach grün (tests/Monarch.Cli.Tests/GateOpenTests.cs, temporäres Repo mit Bare-Remote): Anlage nach Fetch auf neuem origin/develop ohne branch.<b>.remote; aus einer Worktree; Session-Variable verweigert ohne Branch und Worktree; ungültige Eingaben ohne git-Aufruf; Branch existiert schon; kein erwecktes Projekt"
  - "Standardregel 2 und Checkliste in plugin/skills/regeln/SKILL.md nennen ~/.monarch/bin/monarch gate open <G-xxxx> <slug> im Terminal des Owners; RegelnTextTests angepasst; gate cleanup bleibt im Terminal des Owners (Probe ADR-0021 §2)"
  - "Kein anderer Test aufgeweicht; Tests über scripts/test-local.sh"
scope:
  include: ["src/Monarch.Cli/GateOpenCommands.cs", "src/Monarch.Cli/MonarchApp.cs", "tests/Monarch.Cli.Tests/**", "plugin/skills/regeln/SKILL.md", "plugin/skills/gate/SKILL.md", "plugin/README.md", "docs/adr/0021-develop-gruen-und-gate-open.md"]
  exclude: []
estimate: { session_pct: 10, weekly_pct: 3, gold_usd: 0 }
boss: "gate open legt in einem temporären Repo nach dem Fetch die Worktree auf dem neuen origin/develop an, ohne Upstream, und gibt nur den Pfad aus; in einer Claude-Code-Session entsteht nichts"
board: { pr: 168 }
---
# monarch gate open <G> <slug> (#166)
