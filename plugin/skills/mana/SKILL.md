---
name: mana
description: Zeigt Mana (Rate-Limit-Budget) und schaetzt Schritte. Verwenden bei /mana, 'wie viel Budget', vor jedem groesseren Schritt.
---

# Mana

- `~/.monarch/bin/monarch mana`: aktueller Stand (Session-/Wochenfenster, Alter des Snapshots).
- `~/.monarch/bin/monarch mana --estimate`: Schaetzung des naechsten Schritts. **Vor jedem Schritt** aufrufen.
- Die Schaetzung rechnet mit dem Faktor des Paars (Modell, Effort), das der User gewaehlt hat; `mana` zeigt Paar, Faktor und Kalibrierstatus. Monarch gibt dazu keine Empfehlung.
- Ohne Abo (API-Key) gibt es kein Fenster; dann gilt nur das Gold-Budget.
- Am Limit: zu einem sicheren Punkt fertigstellen, `recap`, stoppen. Reset in unter 10 Minuten: pausieren, kein Recap.
- Meldet der Guard „Reset erfolgt, Stand wird erneuert“ (`ResetPending`): einmal `sleep 60` im Vordergrund, dann erneut `mana --estimate`. Das wiederholen, bis `Proceed` kommt oder die letzte Pruefzeit aus der Meldung vorbei ist; dann `recap` und stoppen. Kein Hintergrund-Prozess zum Warten.
- Der Hook verweigert den Start eines Shadows, wenn dessen Kosten nicht mehr passen (`mana_shadow_start`). Im laufenden Shadow verweigert er ab der Grenze jedes Werkzeug (`mana_brake`); der Shadow stoppt dann und meldet seinen Stand als unvollstaendig an die Hauptsession.
- Weckruf nach `rest`: Reset + 1 Minute; liegt der Reset schon zurueck, jetzt + 1 Minute.
- Bei Gates ab Rank B wird bei Knappheit vertagt, nicht herabgestuft.

## Grundregeln (gelten immer)

- CLI nur per Bash: `~/.monarch/bin/monarch ...`. Nie raten, was das CLI ausgeben wuerde.
- Quest-Lifecycle: `quest accept` -> `quest start` -> Arbeit auf dem Gate-Branch `monarch/G-xxxx-<slug>` -> `quest submit` -> `quest pr` -> `evidence sync`. `quest verify` und `quest clear` nur auf Basis von Forge-Evidence (CI gruen, Review), nie aus eigener Behauptung.
- Merge-Regel (ADR-0007): Merge nur nach `develop` (Integrations-Branch) ueber `~/.monarch/bin/monarch gate merge <G-xxxx>`, nie nach main/master (das merged nur der Owner). Rank E-A, nach gruener CI und unabhaengigem Warden-Verdict (`gate verdict`); `gh pr merge` und direkte Pushes auf geschuetzte Branches verweigert der Hook. S: jede veraendernde Aktion einzeln beim Nutzer erfragen.
- Mana-Regel: vor jedem Schritt `~/.monarch/bin/monarch mana --estimate`. Am Limit: bis zu einem sicheren Punkt fertig machen, `~/.monarch/bin/monarch recap`, dann stoppen. Reset in weniger als 10 Minuten: ohne Recap pausieren. Meldet der Guard „Reset erfolgt, Stand wird erneuert“: nach 1 Minute erneut pruefen, bis zur letzten Pruefzeit der Meldung.
- Nie Credential-Dateien lesen (`.env`, Schluessel, Tokens, `~/.ssh`, `~/.aws`, Keychains).
