---
id: Q-0208
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Owner-Entscheid 1 vom 26.11.2026 (Chat): Schicht 3 erkennt den Haupt-Checkout per realpath-Vergleich von git-dir und git-common-dir wie der git-Hook, nicht über das Fehlen von commondir (ProtectMainCheckoutRule.cs:112, HookRunner.cs:532)"
  - "Unklar (commondir unlesbar, leer oder Ziel fehlt) gilt als Haupt-Checkout (wie deny im git-Hook)"
  - "Roter Test zuerst: commondir = . im .git des Haupt-Checkouts, Bash-git-Schreibbefehl und Write werden verweigert, Briefing warnt; danach grün"
  - "Worktree-Tests und bestehende Tests unverändert grün (scripts/test-local.sh bis auf die bekannten Grenzen)"
scope:
  include: ["src/Monarch.Policy/ProtectMainCheckoutRule.cs", "src/Monarch.Cli/HookRunner.cs", "tests/Monarch.Policy.Tests/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 6, weekly_pct: 1.2, gold_usd: 0 }
board: { pr: 163 }
---
# Schicht 3 erkennt den Haupt-Checkout per realpath von git-dir und git-common-dir (commondir)
