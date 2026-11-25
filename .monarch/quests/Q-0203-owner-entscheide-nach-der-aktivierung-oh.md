---
id: Q-0203
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Owner-Entscheid 1 vom 26.11.2026 (Chat): ADR-0020 §1 ohne allowUnixSockets /private/tmp, Settings-Block und python3-Befehl angepasst, Begründung ersetzt"
  - "Owner-Entscheid 3: Standardregel 2 in plugin/skills/regeln/SKILL.md und Deny-Text in ProtectMainCheckoutRule.cs nennen den Owner-Befehl im eigenen Terminal (git -C ~/Projects/umbrarch worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop) und den Start der Session dort; monarch gate open später als eigenes Issue"
  - "Test zum Deny-Text vorher rot, danach grün"
  - "Testergebnisse im Sandbox-Modus vom 26.11.2026 in ADR-0020 §1 mit Befehl und Fehlertext wörtlich"
  - "dotnet build -warnaserror ohne Warnungen; Tests 0 fehlgeschlagen außer belegten Sandbox-Grenzen"
scope:
  include: ["docs/adr/0020-selbst-aufraeumen.md", "plugin/skills/regeln/SKILL.md", "src/Monarch.Policy/ProtectMainCheckoutRule.cs", "tests/Monarch.Policy.Tests/ProtectMainCheckoutTests.cs"]
  exclude: []
estimate: { session_pct: 8, weekly_pct: 2, gold_usd: 0 }
boss: "ProtectMainCheckoutTests.The_deny_text_names_the_way_out erst rot, dann grün"
---
# Owner-Entscheide nach der Aktivierung: ohne Socket-Freigabe, Gate öffnen im Terminal, Testergebnisse Sandbox
