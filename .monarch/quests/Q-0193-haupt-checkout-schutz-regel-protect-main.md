---
id: Q-0193
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen"
  - "Roter Test zuerst, im PR belegt: git checkout main und git commit im Haupt-Checkout eines erweckten Projekts werden heute nicht verweigert"
  - "Neue Regel protect_main_checkout: Deny für git checkout, switch, reset, merge, pull, commit, rebase und stash im Haupt-Checkout eines erweckten Projekts (endgültige Liste laut ADR-0020); git worktree add, worktree remove/list/prune, fetch und die lesenden Befehle (ReadOnlyGit) bleiben erlaubt"
  - "Kandidaten laut ADR-0020 Abschnitt 1 (Vereinigung, anders als FindRepos): FindGitDir je Basis aus cwd, cd, pushd und popd, wobei jedes -C die Basen der Reihe nach ersetzt; dazu jedes --git-dir/GIT_DIR und jeder --work-tree/GIT_WORK_TREE (Arbeitsbaum über FindGitDir, also auch ein Unterordner des Haupt-Checkouts); keiner davon ersetzt die Suche über die Basen. Die Regel greift in Evaluate und in EvaluateProtection, also auch git -C <Haupt-Checkout> commit aus einer Worktree"
  - "Deny-Tests: git --work-tree=/tmp/x checkout main und GIT_DIR=/tmp/y/.git git status; git checkout main, je mit cwd im Haupt-Checkout; git --work-tree=<Haupt-Checkout>/src checkout -f -- . mit cwd in einer Worktree. Allow-Test: git -C <Worktree> commit mit cwd im Haupt-Checkout"
  - "Worktrees desselben Repos und nicht erweckte Repos bleiben unberührt (Tests)"
  - "Deny-Text nennt den Ausweg (Gate-Worktree)"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["src/Monarch.Policy/**", "src/Monarch.Cli/HookRunner.cs", "src/Monarch.Cli/SystemConfig.cs", "tests/Monarch.Policy.Tests/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 16, weekly_pct: 4, gold_usd: 0 }
boss: "Tabellen-Test je verweigertem Befehl und Zielform: deny mit cwd im Haupt-Checkout; deny für cd und -C von einer Worktree aus in den Haupt-Checkout; deny für --git-dir, GIT_DIR, --work-tree (auch auf einen Unterordner) und GIT_WORK_TREE auf den Haupt-Checkout mit cwd in einer Worktree oder einem Ordner ohne Repo; allow, wenn kein Kandidat ein Haupt-Checkout ist (Worktree, nicht erweckte Repos); dazu die vier Fälle aus der DoD; dotnet test grün"
board: { pr: 163 }
---
# Haupt-Checkout-Schutz: Regel protect_main_checkout (#46)
