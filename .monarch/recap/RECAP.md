# RECAP

## Stand
- Zeitpunkt: 2026-11-28T01:35:15Z
- Plan: Pro
- Mana 5h: 17% (Limit 90%, Reset 2026-11-28T05:00:00Z)
- Mana 7d: 91% (Limit 96%, Reset 2026-12-02T11:00:00Z)
- Limits: 5h 90%, 7d 96% (plan default)

## Aktiv
- keine aktive Quest

## Erledigt
- Q-0113 [A] cleared: Merge-Policy nach Zielbranch - https://github.com/DavidOeztuerk/umbrarch/pull/15 - Evidence: https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762702, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762600, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943422084, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943425111, https://github.com/DavidOeztuerk/monarch/actions/runs/40721758194/job/122934234801, https://github.com/DavidOeztuerk/monarch/pull/15
- Q-0127 [C] cleared: Mana-Schätzung je (Modell, Effort) - https://github.com/DavidOeztuerk/umbrarch/pull/15 - Evidence: https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762702, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762600, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943422084, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943425111, https://github.com/DavidOeztuerk/monarch/actions/runs/40721758194/job/122934234801, https://github.com/DavidOeztuerk/monarch/pull/15
- Q-0128 [B] cleared: Weckruf, der einen Session-Neustart überlebt - https://github.com/DavidOeztuerk/umbrarch/pull/15 - Evidence: https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762702, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762600, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943422084, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943425111, https://github.com/DavidOeztuerk/monarch/actions/runs/40721758194/job/122934234801, https://github.com/DavidOeztuerk/monarch/pull/15
- Q-0129 [A] cleared: Wake nur durch den Main (Hook-Regel) - https://github.com/DavidOeztuerk/umbrarch/pull/15 - Evidence: https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762702, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762600, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943422084, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943425111, https://github.com/DavidOeztuerk/monarch/actions/runs/40721758194/job/122934234801, https://github.com/DavidOeztuerk/monarch/pull/15
- Q-0130 [A] cleared: gate merge nie in einen geschützten Branch - https://github.com/DavidOeztuerk/umbrarch/pull/15 - Evidence: https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762702, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122946762600, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943422084, https://github.com/DavidOeztuerk/monarch/actions/runs/40721744761/job/122943425111, https://github.com/DavidOeztuerk/monarch/actions/runs/40721758194/job/122934234801, https://github.com/DavidOeztuerk/monarch/pull/15
- Q-0192 [A] cleared: Design: ADR-0020 Selbst aufräumen (Haupt-Checkout-Schutz, Shadow-Bremse, Reset erfolgt, move-quest, cleanup) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0193 [A] cleared: Haupt-Checkout-Schutz: Regel protect_main_checkout (#46) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0194 [A] cleared: Mana-Bremse für Shadows: Prüfung vor dem Start, Stopp im laufenden Shadow (#161, #74) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0195 [A] cleared: Mana: Fall „Reset erfolgt“ statt RecapAndStop, Weckruf Reset + 1 Minute - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0196 [C] cleared: gate move-quest <Q> --to <G> mit QuestMoved; gate_plan räumt das alte Gate (#16, #81) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0197 [A] cleared: gate cleanup <G> und Gate-Status merged; Monarch-Stand gemergter Gates weitertragen (#44, #36, #46, #139) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0198 [C] cleared: Regeln-, Gate- und Mana-Skill: Kommentar-Regel, Warden-Ablauf, kein --force, neue Befehle - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0199 [A] cleared: Fixes aus der Vorab-Prüfung (F1 bis F6) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0200 [A] cleared: Fixes aus der Warden-Schlussrunde (Major 1 bis 3) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0201 [A] cleared: Haupt-Checkout auf OS- und git-Ebene schützen (Sandbox, reference-transaction, Hook nur Frühwarnung) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0202 [A] cleared: Sandbox in den User-Settings, Hook-Regel enger (HEAD und Branch des Haupt-Checkouts) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0203 [A] cleared: Owner-Entscheide nach der Aktivierung: ohne Socket-Freigabe, Gate öffnen im Terminal, Testergebnisse Sandbox - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0204 [A] cleared: Standardregel 2: Gate-Worktree legt der Owner im Terminal an, Session startet dort - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0205 [A] cleared: Lokale Tests in der Sandbox per Skript, Knights ohne isolation worktree, Aufräumen im Owner-Terminal - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0206 [A] cleared: gate-Skill: gate cleanup vorerst im Terminal des Owners - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0207 [A] cleared: ADR-0020 §1: Probe a4 korrigiert, Probe e für Schicht 2 allein - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0208 [A] cleared: Schicht 3 erkennt den Haupt-Checkout per realpath von git-dir und git-common-dir (commondir) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0209 [A] cleared: Session im Haupt-Checkout: PreToolUse verweigert Bash und Datei-Schreibwerkzeuge (Option B über CLAUDE_PROJECT_DIR) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0210 [A] cleared: ADR-0020 §1: Restrisiko neu, Edit-Regeln, Option B, /private/tmp gemessen, Settings-Befehl - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0211 [A] cleared: Probe i: Edit und Write in der Gate-Worktree gehen trotz Edit-Regeln (ohne Repo-Änderung) - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0212 [A] cleared: ADR-0020 §1: Ergebnisse der Proben f bis i nach der Aktivierung - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0213 [A] cleared: Schicht 3: äußeres .git bestimmt das Repo, Worktree nur unter <common>/worktrees/<name> - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0214 [A] cleared: ADR-0020 §1: vierte Runde, Edit-Regeln für <main>/.git, .git/.git in denyWrite, veraltete Sätze - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0215 [A] cleared: ADR-0020 §1: Ergebnisse der Proben j bis l nach der Aktivierung - https://github.com/DavidOeztuerk/umbrarch/pull/163
- Q-0216 [A] cleared: develop grün für gate merge: R1 Nachtläufe zählen nicht, R3 evidence.required (#167) - https://github.com/DavidOeztuerk/umbrarch/pull/168
- Q-0217 [B] cleared: monarch gate open <G> <slug> (#166) - https://github.com/DavidOeztuerk/umbrarch/pull/168
- Q-0218 [A] cleared: Versionspflicht für plugin/ und Version 0.1.3 - https://github.com/DavidOeztuerk/umbrarch/pull/168

## Offen
- Q-0148: Design zuerst (einfache Sprache, im PR): docs/adr/0015-relatives-latenz-gate.md. Die CI misst auf demselben Runner, verschränkt in Blöcken (Bezug und Szenario abwechselnd), den reinen Programmstart `monarch --version` als Bezug und gatet je Szenario den Aufschlag des Hooks auf diesen Bezug am Median (p50); der Bezug läuft im selben Modus wie das Szenario (direkt: Binary, Wrapper: plugin/scripts/monarch). Das ADR nennt, dass `--version` über System.CommandLine läuft und der Hook es umgeht (src/Monarch.Cli/MonarchApp.cs:12-28), misst diesen Unterschied und begründet den Bezug; ein anderer Bezug nur nach Owner-Entscheid. Der Aufschlag ist so definiert, dass ein gleichmäßig langsamerer Runner ihn nicht verschiebt (gemessen: in-scope p50 5,67 ms in Lauf 41938370015 gegen 8,27 ms in Lauf 42218379672; Verhältnis denied-bash/in-scope stabil 1,55-1,70). Schwelle je Szenario aus gemessenen Läufen (Zahlen und Run-IDs, beide OS, direkt und Wrapper), nicht geschätzt; denied-bash und out-of-scope tragen den Ledger-Append nach der geflushten Entscheidung (HookRunner.cs:199-222). Nachweis, dass ein echter Regress auffällt (z. B. ein zusätzlicher Prozessstart auf dem heißen Pfad). Eine Warden-Design-Runde plus höchstens eine Delta-Runde, Verdict vor dem ersten Code-Commit
- Q-0148: Lockerung offen benannt: p99 <= 20 ms bleibt unverändert das Kriterium auf der Zielmaschine (MVP-Gate C1,
- Q-0148: bench/test-gate.sh erweitert, vorher rot, danach grün, deterministisch über BENCH_RESULTS_DIR mit Bezugs-Samples: synthetische Regression nur auf den Hook-Samples -> rot; gleichmäßig langsamerer Runner (Bezug und Hook mit mindestens dem gemessenen Runner-Faktor ≈ 1,5 skaliert) -> grün; Einzelausreißer -> grün; bestehende Prüfungen (p95 besteht, p99 fällt, p95-Spalte; test-gate.sh:20-30) unverändert
- Q-0148: Echte Fehler-Injektion: ein CI-Lauf auf dem Gate-Branch mit Verzögerungs-Wrapper nur im Bench (kein src-Code) -> relativer Schritt rot auf beiden OS; Run-ID im ADR; im finalen Head ist die Injektion nicht aktiv
- Q-0148: ci.yml: neuer relativer Schritt blockierend auf Linux und macOS, direkt und über den Wrapper; die macOS-Quarantäne (Kommentar :82-87, continue-on-error :89, Summary-Zeile :95) entfällt, weil der relative Schritt den Wrapper-Modus blockierend abdeckt; der absolute Direkt-Schritt (:80-81) und der Wrapper-Schritt (:88-101) berichten nur noch; der Selbsttest (:78-79) bleibt blockierend; permissions bleiben contents: read (:10-11). Beleg: grüner CI-Lauf auf dem Gate-Branch mit dem relativen Gate auf beiden OS, Run-ID und Zahlen im ADR oder PR
- Q-0148: Verweise: ADR-0011 Regel 9 (:70) und der ci.yml-Kommentar (:51-53) zeigen auf ADR-0015; docs/lessons.md bekommt einen kurzen Nachtrag (Lockerung offen benannt, C1 weiter offen)
- Q-0148: Heikle Quest (Standardregel 3: CI-Gate gelockert, Owner-Entscheid 11.11.2026): kein src-Code; kein anderer Test und keine andere Schwelle aufgeweicht; Warden-Runde über den Code mit Verdict am Head-SHA. Abhängigkeit: keine (technisch unabhängig von Q-0149)
- Q-0150: Design zuerst (einfache Sprache, im PR): Nachtrag in docs/adr/0015-relatives-latenz-gate.md. Er nennt den Owner-Entscheid vom 12.11.2026 wörtlich (20 ms p99 auf ruhiger Maschine; unter voller Last ein relativer Faktor gegen monarch --version) und legt ihn nicht neu aus. Er enthält die Messwerte vom 12.11.2026 (abwechselnde Blöcke, über den Wrapper mit installiertem Binary, Load und Prozesse je Block), Median, p95 und Streuung des Verhältnisses und daraus einen vorgeschlagenen Faktor je Klasse. Der Faktor ist als Vorschlag markiert, bis der Owner ihn übernimmt
- Q-0150: Grenzen der Messung offen benannt: unter welcher Last gemessen wurde, was davon künstliche Last war und was das relative Kriterium nicht erkennt (ein insgesamt langsameres Binary; dafür bleibt Teil 1 von C1 auf ruhiger Maschine)
- Q-0150: bench/hook-latency.sh bekommt einen eigenen, benannten Modus für C1 unter Last mit eigenen, fest im Skript stehenden Schwellen, getrennt von den CI-Schwellen. Die CI-Schwellen und das Verhalten von GATE_MODE absolute und relative bleiben unverändert. Der neue Modus lässt sich nicht nutzen, um das CI-Gate zu lockern: nur auf macOS, Schwellen nicht über die Umgebung änderbar, ungültige Eingaben sind Exit 2
- Q-0150: bench/test-gate.sh erweitert, vorher rot, danach grün, deterministisch über BENCH_RESULTS_DIR: knapp unter den C1-Schwellen grün, knapp darüber rot je Klasse; Schwellen nicht über die Umgebung anhebbar; auf einem anderen Betriebssystem Exit 2; alle bestehenden Prüfungen unverändert grün
- Q-0150: Verweise: der Kommentar zu C1 in .github/workflows/ci.yml nennt die Bedingung ruhige Maschine und den Nachtrag; docs/lessons.md bekommt einen kurzen Nachtrag; die CI-Schritte selbst ändern sich nicht
- Q-0150: Heikle Quest nach Standardregel 3: Warden-Runde über das Design vor dem Code, kein src-Code, kein anderer Test und keine andere Schwelle aufgeweicht, Schlussrunde des Wardens mit Verdict am Head-SHA

## Nächster konkreter Schritt
Option B für #17/#18 (ohne Code begonnen): Policy braucht $TMPDIR als Wurzel (PolicyContext hat keinen Temp-Pfad, ToolCallPolicy.cs ScopeLock Zeile ~536, ShadowReadOnly ~495). Rote Tests zuerst: Schreiben unter $TMPDIR erlaubt; $TMPDIR/../x, Symlink aus $TMPDIR in die Root, /etc/hosts, ../other/src/a.cs bleiben Deny. Dann #105 prüfen (Test-Repos unter $TMPDIR?). Dann #26 (plugin Warden-Export ':!.monarch'). Danach Warden-Schlussrunde (sonnet) über PR #182 auf dem neuen Head, gate verdict, gate merge, gate check, evidence sync je Quest, ledger verify. Head PR 182: 54f62e8.

## Offene Entscheidungen
- Mana-Reserve 7d etwa 3 pp: Gate-Abschluss (Schlussrunde, Merge) hat Vorrang vor #17/#18/#26/#105 im nächsten Lauf.
