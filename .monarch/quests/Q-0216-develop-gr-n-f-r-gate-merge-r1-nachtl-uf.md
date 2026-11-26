---
id: Q-0216
gate: G-0131
kind: main
rank: A
state: in_progress
dod:
  - "Design zuerst: ADR-0021 §1 mit den Owner-Entscheiden vom 26.11.2026 wörtlich; schmale Warden-Design-Runde nur über §1 mit approve vor dem ersten Code-Commit, höchstens eine Delta-Runde"
  - "R3: .monarch/system.yaml:30-31 (evidence.required mit build-test (ubuntu-latest), plugin, aot (linux-x64)) vom Owner eingetragen, lesend geprüft und mit Q-0216 committet"
  - "R1: CheckCi zählt keine Checks aus Workflow-Läufen mit event schedule, auch nicht laufende; das Event kommt aus GET actions/runs?head_sha über check_suite.id; scheitert die Abfrage, verweigern quest verify und gate merge (fail-closed); R2 wird nicht gebaut (Owner-Entscheid 2)"
  - "Tests vorher rot, danach grün (ADR-0021 §1 Tests 1-7): Fall aus #167 nur mit R1 und mit R3; roter Pflicht-Check nur im Nachtlauf; Push rot und Nachtlauf grün bleibt rot; Pflicht-Check nur im Nachtlauf ist Pending; roter Check außerhalb der Liste blockiert nicht; actions/runs-Fehler verweigert; Forge.Required liest die drei Namen aus der Fluss-Liste"
  - "Heikle Quest (Merge-Schutzregel): kein anderer Test und keine Schwelle aufgeweicht; bestehende Tests unverändert grün über scripts/test-local.sh; Fake in Part2Support.cs liefert für actions/runs im Default eine leere Liste"
scope:
  include: ["docs/adr/0021-develop-gruen-und-gate-open.md", ".monarch/system.yaml", "src/Monarch.Infrastructure.Evidence/**", "src/Monarch.Application/Ports/IEvidenceSource.cs", "src/Monarch.Cli/EvidenceCommands.cs", "src/Monarch.Cli/GateMergeCommands.cs", "tests/Monarch.Infrastructure.Tests/**", "tests/Monarch.Cli.Tests/**"]
  exclude: []
estimate: { session_pct: 15, weekly_pct: 4, gold_usd: 0 }
boss: "gate merge hält develop am Fall aus #167 (alter Nachtlauf mit Platzhalter skipped, neuere Läufe grün) für grün, mit R1 allein und mit R3; Push rot plus Nachtlauf grün bleibt rot"
---
# develop grün für gate merge: R1 Nachtläufe zählen nicht, R3 evidence.required (#167)
