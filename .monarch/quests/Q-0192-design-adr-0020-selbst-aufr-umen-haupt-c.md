---
id: Q-0192
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "ADR-0020 in einfacher Sprache deckt alle Teile ab: Haupt-Checkout-Schutz (Befehlsliste, Ausnahmen, Zielermittlung), Shadow-Bremse (Prüfung vor dem Start, Verweigerung im laufenden Shadow, Freshness, Default ohne Messwert), Fall „Reset erfolgt“ mit Obergrenze und Weckzeit, gate move-quest, gate cleanup, Gate-Status merged und Weg des Monarch-Stands gemergter Gates (#139)"
  - "Messung der Kosten je Shadow mit Belegen: Befehl, Ledger-Quellen, Filter (Plan Pro, ohne Überlappung, Snapshot höchstens 2 min vor Start und nach Ende), n, Median, p90 und Max je Shadow und Modell"
  - "Owner-Vorgaben nur wörtlich mit Datum und Quelle (CLAUDE.md Regel 4); alles andere als „Vorschlag“ oder „OFFEN“ markiert, Zusammenfassungen als „Zusammenfassung“"
  - "Jede Fundstelle mit Datei, Zeile und Befehl (CLAUDE.md Regel 5)"
  - "Warden-Designrunde vor dem ersten Code-Commit des Gates; höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
  - "Enthält die ADR Owner-Entscheide: nach der Owner-Freigabe braucht jede Änderung eine neue Freigabe (CLAUDE.md Regel 3)"
scope:
  include: ["docs/adr/0020-*.md"]
  exclude: []
estimate: { session_pct: 22, weekly_pct: 5, gold_usd: 0 }
boss: "Warden-Designverdict ohne offene Punkte nach höchstens einer Delta-Runde"
board: { pr: 163 }
---
# Design: ADR-0020 Selbst aufräumen (Haupt-Checkout-Schutz, Shadow-Bremse, Reset erfolgt, move-quest, cleanup)
