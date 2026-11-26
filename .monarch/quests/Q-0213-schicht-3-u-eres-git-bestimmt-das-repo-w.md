---
id: Q-0213
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Owner-Entscheid 2 vom 26.11.2026 (Chat): Ein inneres .git verdeckt kein äußeres (realpath, die oberste .git-Komponente bestimmt das Repo; eine Worktree-Datei .git bleibt die Grenze)"
  - "Minor 2 im selben Fix: Als Worktree zählt nur ein git-dir unter <common>/worktrees/<name>; alles andere zählt als Haupt-Checkout"
  - "Owner-Entscheid 4: Briefing-Text HookRunner.cs:546 korrigiert"
  - "Rote Tests zuerst; danach grün; scripts/test-local.sh bis auf die bekannten Grenzen"
scope:
  include: ["src/Monarch.Policy/ProtectMainCheckoutRule.cs", "src/Monarch.Cli/HookRunner.cs", "tests/Monarch.Policy.Tests/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 6, weekly_pct: 1.2, gold_usd: 0 }
board: { pr: 163 }
---
# Schicht 3: äußeres .git bestimmt das Repo, Worktree nur unter <common>/worktrees/<name>
