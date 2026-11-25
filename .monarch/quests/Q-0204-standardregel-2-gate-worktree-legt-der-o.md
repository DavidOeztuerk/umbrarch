---
id: Q-0204
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Owner-Entscheid 3 vom 26.11.2026 (Chat): Standardregel 2 in plugin/skills/regeln/SKILL.md nennt den Owner-Befehl im eigenen Terminal (git -C ~/Projects/umbrarch worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop) und den Start der Session dort, statt git worktree add plus EnterWorktree durch die Session"
  - "Checkliste 'Reihenfolge je Gate' Schritt 1 entsprechend; gate carry bleibt direkt nach dem Start"
  - "RegelnTextTests: Checklisten-Test verlangt den Owner-Schritt statt EnterWorktree, vorher rot, danach grün; keine andere Prüfung aufgeweicht"
scope:
  include: ["plugin/skills/regeln/SKILL.md", "tests/Monarch.Cli.Tests/RegelnTextTests.cs"]
  exclude: []
estimate: { session_pct: 4, weekly_pct: 1, gold_usd: 0 }
boss: "RegelnTextTests.Skill_has_the_gate_checklist_in_the_required_order erst rot, dann grün"
---
# Standardregel 2: Gate-Worktree legt der Owner im Terminal an, Session startet dort
