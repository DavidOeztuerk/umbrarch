---
id: Q-0210
gate: G-0130
kind: side
rank: A
state: in_review
dod:
  - "Owner-Entscheid 2 vom 26.11.2026: Restrisiko im ADR und in plugin/README.md neu formuliert, nicht mehr auf packed-refs ausgedehnt; packed-refs und commondir in denyWrite"
  - "Doku-Belege wörtlich: Edit-Regeln decken alle Datei-Werkzeuge ab, Zusammenführung mit der Sandbox, CLAUDE_PROJECT_DIR bleibt die Startwurzel"
  - "Messung, ob Build und Test-Apps nach /private/tmp schreiben, mit Befehl und Ergebnis"
  - "Befehl für den Owner (Sicherung, Abbruch, Prüfung), an Kopien getestet; Proben f bis h als Plan"
scope:
  include: ["docs/adr/0020-selbst-aufraeumen.md", "plugin/README.md"]
  exclude: []
estimate: { session_pct: 6, weekly_pct: 1.2, gold_usd: 0 }
---
# ADR-0020 §1: Restrisiko neu, Edit-Regeln, Option B, /private/tmp gemessen, Settings-Befehl
