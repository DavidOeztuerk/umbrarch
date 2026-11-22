---
id: Q-0193
gate: G-0130
kind: main
rank: A
state: proposed
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen"
  - "Roter Test zuerst, im PR belegt: git commit im Haupt-Checkout eines erweckten Projekts wird heute nicht verweigert"
  - "Neue Regel protect_main_checkout: Deny für git checkout, switch, reset, merge, pull, commit, rebase und stash im Haupt-Checkout eines erweckten Projekts (endgültige Liste laut ADR-0020); git worktree add, worktree remove/list/prune, fetch und die lesenden Befehle (ReadOnlyGit) bleiben erlaubt"
  - "Ziel wie bei FindRepos (ProtectBranchesRule.cs:205-245) aus cwd, cd und -C; die Regel greift in Evaluate und in EvaluateProtection, also auch git -C <Haupt-Checkout> commit aus einer Worktree"
  - "Worktrees desselben Repos und nicht erweckte Repos bleiben unberührt (Tests)"
  - "Deny-Text nennt den Ausweg (Gate-Worktree)"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["src/Monarch.Policy/**", "src/Monarch.Cli/HookRunner.cs", "src/Monarch.Cli/SystemConfig.cs", "tests/Monarch.Policy.Tests/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 16, weekly_pct: 4, gold_usd: 0 }
boss: "Tabellen-Test: jeder verweigerte Befehl je Zielform (cwd, cd, -C) im Haupt-Checkout deny, in der Worktree allow; dotnet test grün"
---
# Haupt-Checkout-Schutz: Regel protect_main_checkout (#46)
