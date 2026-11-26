---
id: Q-0205
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Owner-Entscheid 3 vom 26.11.2026 (Chat): scripts/test-local.sh baut mit --disable-build-servers -m:1 -nodeReuse:false und startet die vier Test-Apps direkt; Exitcode ungleich 0, wenn eine App fehlschlägt; der /bin/ps-Test wird weder aufgeweicht noch ausgefiltert, das Skript nennt ihn als bekannte Grenze der Sandbox; CI unverändert"
  - "Standardregel und Knight: lokale Tests über scripts/test-local.sh"
  - "Owner-Entscheid 4: Regel in den Skills, dass Knights nie mit isolation: worktree laufen (Basis origin/HEAD = main)"
  - "Owner-Entscheid 5: Aufräumen nach dem Merge (gate cleanup) macht vorerst der Owner im eigenen Terminal; SKILL.md Regel 2 und Checkliste angepasst"
  - "RegelnTextTests prüft die neuen Regeln, vorher rot, danach grün; keine Prüfung aufgeweicht"
  - "ADR-0020 §1: Owner-Entscheide wörtlich, Ergebnisse TLS-Nachtest und Proben"
scope:
  include: ["scripts/test-local.sh", "plugin/skills/regeln/SKILL.md", "plugin/skills/quest/SKILL.md", "plugin/agents/knight.md", "tests/Monarch.Cli.Tests/RegelnTextTests.cs", "docs/adr/0020-selbst-aufraeumen.md"]
  exclude: []
estimate: { session_pct: 8, weekly_pct: 2, gold_usd: 0 }
boss: "RegelnTextTests erst rot, dann grün; scripts/test-local.sh liefert Exit ungleich 0 bei dem bekannten /bin/ps-Fehler"
board: { pr: 163 }
---
# Lokale Tests in der Sandbox per Skript, Knights ohne isolation worktree, Aufräumen im Owner-Terminal
