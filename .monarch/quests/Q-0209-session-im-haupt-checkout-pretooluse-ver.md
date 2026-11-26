---
id: Q-0209
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Owner-Entscheid 3 vom 26.11.2026 (Chat): Ist realpath von CLAUDE_PROJECT_DIR ein Haupt-Checkout, verweigert PreToolUse jeden Bash-Aufruf und Edit/Write/MultiEdit/NotebookEdit mit dem Grund \"Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu\"; Lesen bleibt erlaubt"
  - "Auch wenn das cwd außerhalb jedes erweckten Projekts liegt"
  - "Roter Test zuerst; danach grün; ohne CLAUDE_PROJECT_DIR und mit einer Worktree als Wurzel kein Deny dieser Art"
scope:
  include: ["src/Monarch.Policy/ProtectMainCheckoutRule.cs", "src/Monarch.Policy/ToolCallTypes.cs", "src/Monarch.Cli/HookRunner.cs", "tests/Monarch.Policy.Tests/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 8, weekly_pct: 1.6, gold_usd: 0 }
board: { pr: 163 }
---
# Session im Haupt-Checkout: PreToolUse verweigert Bash und Datei-Schreibwerkzeuge (Option B über CLAUDE_PROJECT_DIR)
