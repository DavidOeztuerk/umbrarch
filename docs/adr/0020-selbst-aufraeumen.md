# ADR-0020: Selbst aufräumen (v0.2 Teil 1)

- Status: Design vom Warden freigegeben für develop (schmale Prüfung des Fixes, approve, PR #163, Kommentar `#issuecomment-7079968810`); Owner-Fragen siehe „Offen“
- Datum: 2026-11-22
- Gate: G-0130, Quests Q-0192 bis Q-0198
- Bezug: #46, #161, #74, #139, #16, #81, #44, #36, ADR-0003, ADR-0004, ADR-0007, ADR-0008, ADR-0010, ADR-0011, ADR-0012, ADR-0015, ADR-0018, ADR-0019
- Fassung: Designrunde 1 (`#issuecomment-7078646801`), Delta-Runde (`#issuecomment-7079794677`), Fix und schmale Prüfung (`#issuecomment-7079968810`)

## Owner-Vorgaben

Wörtlich aus der Prompt des Owners vom 22.11.2026 (Gate „v0.2 Teil 1: Selbst aufräumen“):

> 1. Schutzlücke Haupt-Checkout (#46): Der Hook verweigert schreibende Git-Befehle (checkout, switch, reset, merge, pull, commit, rebase, stash) im Haupt-Checkout eines erweckten Projekts. Ausnahme: worktree add. Roter Test zuerst.
> 2. Mana Priorität 1 (#161, #74): (a) Der Hook prüft vor dem Start eines Shadows anhand gemessener Kosten je Shadow und Modell aus den Ledger-Snapshots, sonst mit konservativem Default. (b) Ab der Grenze verweigert der Hook weitere Werkzeuge im laufenden Shadow. (c) Prüfen, ob der Mana-Stand im langen Shadow-Lauf frisch wird. Erst messen, dann Schwellen setzen, keine Schwelle ohne Beleg aus dem Ledger.
> 3. Weckruf nach Reset: Ist resets_at vorbei, meldet der Guard „Reset erfolgt, Stand wird erneuert“ statt RecapAndStop. Die Session prüft nach 1 Minute erneut, mit Obergrenze. Weckruf auf Reset + 1 Minute.
> 4. Status auf develop nach gate merge (#139): Gate- und Quest-Dateien auf develop auf den Stand nach dem Merge bringen.
> 5. gate move-quest <Q> --to <G> (#16, #81): ändert beide Gate-Dateien und die Quest-Datei in einem Schritt und schreibt ein Ledger-Ereignis. gate_plan entfernt verschobene Quests aus dem alten Gate.
> 6. gate cleanup <G> (#44, #36, #46): committet den Ledger-Nachtrag im Worktree mit [skip ci], pusht und entfernt den Worktree ohne --force, alles per git -C. Nie --force vorschlagen.

Alles Weitere in diesem Dokument ist technisches Design der Session. Wo es über den Wortlaut hinausgeht, steht „Vorschlag“. Was der Owner entscheiden muss, steht unter „Offen“.

## Zusammenfassung (einfache Sprache)

1. **Haupt-Checkout:** Im Hauptordner eines Projekts mit Monarch lehnt der Hook acht schreibende Git-Befehle ab. Gearbeitet wird nur in der Gate-Worktree. Neue Worktrees anlegen geht weiter.
2. **Mana bei Shadows:** Vor dem Start eines Shadows prüft der Hook, ob dessen typische Kosten noch unter die Grenze passen. Die Kosten sind aus den Ledgern gemessen. Erreicht ein laufender Shadow die Grenze, bekommt er keine Werkzeuge mehr und meldet seinen Stand als unvollständig. Ohne frischen Stand (headless) bremst nur der letzte bekannte Wert.
3. **Nach dem Reset:** Zeigt der Stand ein Fenster, das schon zurückgesetzt ist, sagt Monarch „Reset erfolgt, Stand wird erneuert“. Die Session prüft dann jede Minute neu, höchstens 10 Minuten lang (Vorschlag). Der Weckruf kommt eine Minute nach dem Reset (selten zwei, wegen der Jitter-Minuten) statt nach drei.
4. **Status auf develop:** develop nimmt keinen Commit ohne grüne Pflicht-Checks an. Der Stand nach dem Merge kommt darum erst mit dem nächsten Gate nach develop: Ein neuer Befehl holt ihn in die neue Worktree (Vorschlag, Offen 2).
5. **Quest verschieben:** Ein Befehl hängt eine Quest in ein anderes Gate um, in beiden Gate-Dateien und in der Quest-Datei, und schreibt das ins Ledger.
6. **Aufräumen:** Ein Befehl erledigt das Aufräumen nach dem Merge: committen, pushen, Worktree entfernen, ohne `--force`.

## 1. Regel protect_main_checkout

**Was zählt.** Ein Bash-Segment mit dem Wort `git` und einem dieser Unterbefehle: `checkout`, `switch`, `reset`, `merge`, `pull`, `commit`, `rebase`, `stash`. Den Unterbefehl bestimmt `BashAnalysis.GitSubcommand` wie bei `protect_branches`; globale Optionen wie `-c` oder `--no-pager` davor zählen nicht. `git stash list` und `git stash show` lesen nur und zählen nicht (Vorschlag: Lesart von „schreibende Git-Befehle“).

**Wo der Befehl läuft.** Die Verzeichnisse vor dem Segment (cwd, `cd`, `pushd`, `popd` aus dem cwd-Modell), danach jedes `-C <dir>` der Reihe nach. Ein `env -C <dir>` oder `env --chdir=<dir>` direkt vor `git` zählt wie `git -C <dir>`, auch in getopt-Schreibweise (`-C<dir>`, gebündelt `-iC <dir>`, gekürzt `--ch <dir>`); je `env` gilt das letzte `-C`. Vor `sh -c`, `bash -c` und einer Shell, die einen Heredoc liest, gilt `<dir>` als Startverzeichnis ihres Codes; bei `… <<EOF | env -C <dir> sh` zählt das Verzeichnis der Shell hinter der Pipe, nicht das des Befehls davor. Quelle: Warden-Code-Runde und Delta-Runde zu Q-0193 (`env -C`, getopt-Formen, `sh -c`); Kürzung, Heredoc und Pipe-Fall aus der Prüfung des Kritikers. Der Wert von `env -C` gilt wörtlich, auch wenn er mit `-` beginnt; ein `cd` mit unbekannter Option (etwa `cd -d`) scheitert wie in bash, das Verzeichnis bleibt, und nach `cd --` gilt das nächste Wort wörtlich (schmale Prüfung Q-0193, `#issuecomment-7083652257`). Die Regel prüft eine Vereinigung von Kandidaten und verweigert, sobald einer davon ein Haupt-Checkout ist:
- das Git-Verzeichnis je Basis über `BranchGuard.FindGitDir`. Basen sind die Verzeichnisse aus dem cwd-Modell (cwd, `cd`, `pushd`, `popd`). Jedes `-C <dir>` ersetzt die Basen der Reihe nach, wie bei `FindRepos` (`ProtectBranchesRule.cs:215-223`), denn Git arbeitet dann in `<dir>` und nicht im cwd. Lässt sich ein `-C` nicht auflösen, bleiben die bisherigen Basen stehen (`:223`), mit cwd im Haupt-Checkout also ein Deny;
- jedes ausdrückliche Git-Verzeichnis aus `--git-dir` im Segment oder `GIT_DIR` aus irgendeinem Segment;
- jeden ausdrücklichen Arbeitsbaum aus `--work-tree` im Segment oder `GIT_WORK_TREE` aus irgendeinem Segment.

Anders als bei `FindRepos` (`ProtectBranchesRule.cs:205-245`, dort ersetzen `--git-dir` und `GIT_DIR` die Suche, `:226-241`, und `--work-tree` wird nicht ausgewertet) ersetzen `--git-dir`, `GIT_DIR`, `--work-tree` und `GIT_WORK_TREE` hier die Suche über die Basen nicht, sie kommen als Kandidaten hinzu. Relative Werte gelten wie bei `FindRepos` gegen die Basen nach `-C` (`:236-238`). So bleibt `git --work-tree=/tmp/x checkout main` im Haupt-Checkout ein Deny, denn Git nimmt das Git-Verzeichnis dann aus dem cwd. Dasselbe gilt für `GIT_DIR=/tmp/y/.git git status; git checkout main`. Folge der Vereinigung: Ein Befehl mit `GIT_DIR` einer Worktree, aber cwd im Haupt-Checkout, wird ebenfalls verweigert (streng). Erlaubt bleibt `git -C <Worktree> commit` mit cwd im Haupt-Checkout, denn `-C` ersetzt die Basis. Das braucht das Aufräumen von Hand nach Standardregel 2, bis `gate cleanup` es übernimmt (Abschnitt 6).

**Was ein Haupt-Checkout ist.** Das gefundene Git-Verzeichnis heißt `.git`, ist ein Verzeichnis und hat keine Datei `commondir`, ist also kein verknüpfter Worktree. Der Ordner darüber enthält ein Verzeichnis `.monarch` (erwecktes Projekt). Für `--work-tree` und `GIT_WORK_TREE`: `BranchGuard.FindGitDir(<Arbeitsbaum>)` und derselbe Test. So zählt auch ein Unterordner des Haupt-Checkouts, etwa `git --work-tree=<Haupt-Checkout>/src checkout -f -- .` aus einer Worktree. Ein Arbeitsbaum ohne Git-Verzeichnis darüber (etwa `/tmp/x`) ergibt keinen Kandidaten. Ein Worktree unter `.claude/worktrees/` hat eine `.git`-Datei. Sein Git-Verzeichnis liegt unter `.git/worktrees/<name>` und zählt nie.

**Entscheidung.** Deny mit Regel `protect_main_checkout` und dem Weg: „Der Haupt-Checkout bleibt unverändert (Standardregel 2). Arbeite in der Gate-Worktree: `git worktree add .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop`, dann EnterWorktree.“ Das gilt für Main und Shadows gleich. Die Regel läuft in `Evaluate` und in `EvaluateProtection`, also auch, wenn der Haupt-Checkout nur Ziel eines Befehls ist, etwa `git -C ~/Projects/Noelia checkout main` aus einer anderen Session.

**Ausnahme.** `git worktree add` zählt nicht, weil `worktree` nicht in der Liste steht. Dasselbe gilt für die anderen `worktree`-Unterbefehle (`list`, `remove`, `prune`). `worktree remove` braucht das Aufräumen (Abschnitt 6).

**Was die Regel nicht prüft (Grenzen nach ADR-0011).** Git ohne das Wort `git` in einem erkannten Segment (Alias, Wrapper-Skript, `xargs git`) und Pfade aus Variablen oder `$(…)`, die das cwd-Modell nicht auflöst. Den Inhalt von `sh -c "…"` zerlegt `BashAnalysis` in Segmente (`BashAnalysis.cs:99-103`); er wird also geprüft.

**Weitere Folgen.**
- Test-Repos mit einem Ordner `.monarch` gelten als erweckte Projekte. Ein Agent kann dort im Haupt-Checkout nicht mehr committen. Auch `git checkout -f -- <Pfad>` geht nicht mehr, obwohl `plugin/agents/knight.md:20` und `warden.md:19` es in Test-Repos erlauben. Für den Warden ist nur das Letzte neu, `git commit` verweigert ihm der Hook schon heute (`ToolCallPolicy.cs:455-462`). Policy-Fälle laufen ohnehin als Repo-Tests (Owner 16.11.2026). Die Texte der Agenten passen das Issue „Nacharbeit G-0130“ an.
- Der erste Commit nach `awaken` in einem neuen Projekt liegt im Haupt-Checkout. Ihn macht der Owner selbst, oder die Session legt zuerst eine Worktree an.
- `~/.monarch` ist kein Git-Repo und kein erwecktes Projekt; die Regel trifft es nicht. Andere schreibende Git-Befehle (`add`, `restore`, `clean`, `cherry-pick`, `revert`, `am`, `apply`, `mv`, `rm`, `branch -f`, `tag`, `update-ref`) stehen nicht in der Liste des Owners und sind nicht gesperrt (Offen 1).

**Tests.** Q-0193 beginnt mit einem roten Test (Vorgabe „Roter Test zuerst“): `git checkout main` und `git commit` im Haupt-Checkout eines erweckten Testprojekts werden heute erlaubt. Dazu kommen Deny-Tests für `git --work-tree=/tmp/x checkout main` und `GIT_DIR=/tmp/y/.git git status; git checkout main`, je mit cwd im Haupt-Checkout, und für `git --work-tree=<Haupt-Checkout>/src checkout -f -- .` mit cwd in einer Worktree. Ein Allow-Test belegt `git -C <Worktree> commit` mit cwd im Haupt-Checkout.

**Folge für den Owner.** Befehle, die der Owner selbst im Terminal oder mit `!` ausführt, laufen nicht durch den Hook. `git pull` im Haupt-Checkout bleibt so ein Owner-Befehl.

## 2. Mana-Bremse für Shadows

### Messung

Quelle: alle Ledger-Zeilen aus umbrarch, Noelia und workertransfer, aus den Arbeitsordnern und allen Branches (lokal und `origin`), dedupliziert nach Datei, `seq` und Hash; 14 941 Ereignisse, Stand 22.11.2026. Alle drei Projekte stehen auf Plan Pro (`.monarch/system.yaml` Zeile 6; in umbrarch seit Commit `5c9f83e` vom 28.10.2026). Die Messung lief am 22.11.2026 mit Skripten aus dem Scratchpad der Session. Aus dem Repo lässt sie sich erst wiederholen, wenn Q-0194 dieselbe Logik mit genau diesen Kriterien nach `tools/shadow-costs.py` (nur lesend) bringt.

Das Modell eines Shadows steht nicht im Ledger. Die Zuordnung kommt aus den heutigen Agent-Definitionen (`plugin/agents/*.md` Zeile 4, unverändert seit Commit `a5b3cd2` vom 28.10.2026). Läufe mit anderem Modell per `model`-Parameter wären falsch zugeordnet (Annahme).

Kriterien (verbindlich auch für `tools/shadow-costs.py`):
- Ein Shadow-Lauf beginnt mit dem ersten `ToolUsed` oder `PolicyDecided` seiner `agent_id` und endet mit `ShadowRunCompleted`.
- Gemessen wird `fiveHourUsedPct` zwischen dem letzten frischen `ManaSnapshotTaken` vor dem Start und dem ersten frischen nach dem Ende, beide im selben 5h-Fenster.
- Ein Lauf zählt nur, wenn alle folgenden Bedingungen gelten:
  - Beide Abstände (Snapshot bis Start, Ende bis Snapshot) sind höchstens 2 Minuten, ungerundet.
  - Kein anderer Shadow-Lauf überlappt.
  - Kein anderer Lauf nutzt dasselbe Messpaar.
  - Die 7d-Differenz ist nicht negativ, es lag also kein Wochen-Reset dazwischen.

Ergebnis (Nachrechnung der Warden-Designrunde 1 mit ungerundeten Abständen, PR-Kommentar `#issuecomment-7078646801`, Befund 3):

| Shadow (Modell laut `plugin/agents/*.md`) | n | 5h Median | 5h p90 | 5h Max | 7d p90 |
|---|---|---|---|---|---|
| warden (opus) | 13 | 10 | 18 | 36 | 3 |
| knight (sonnet) | 9 | 5 | 8 | 19 | 1 |
| scout (sonnet) | 1 | – | – | – | – |
| strategist (opus) | 0 | – | – | – | – |

Die erste Auswertung der Session hatte die Abstände auf ganze Minuten gerundet (bis unter 2,5 Minuten) und kam auf 28 Läufe (warden n=14, p90 21; knight n=11, p90 12). Fünf dieser Läufe liegen ungerundet über 2 Minuten. Es gilt die strenge Fassung oben. Q-0194 wiederholt die Rechnung im Repo; weicht das Skript ab, gilt seine Tabelle, und der ADR wird in einer schmalen Prüfung angepasst.

Für kritiker und fremde Agenten (`general-purpose` u. a.) gibt es keinen sauberen Lauf.

**(c) Wird der Stand im langen Lauf frisch?** Ja. Beleg (Noelia, 21.11.2026): Die Hauptsession benutzt um 21:13:24 zuletzt ein Werkzeug (Session-Ledger `be145c6d…`, seq 179), der Warden endet um 21:29:30 (seq 209). Dazwischen ruft der Warden selbst `monarch mana` auf: `cli-20261121`, seq 30 um 21:21:17 mit 47 % und seq 31 um 21:25:48 mit 50 %, beide `stale: false`; vor dem Start (seq 29, 21:13:20) waren es 37 %. Ohne Aktivität der Hauptsession kann ein frischer Wert nach mehr als 10 Minuten nur aus einer Statusline während des Shadow-Laufs stammen (`ManaStore.StaleAfter`, `State.cs:58`).

### (a) Prüfung vor dem Start

- **Matcher (Vorschlag):** Der PreToolUse-Matcher in `plugin/hooks/hooks.json:16` bekommt `Agent|Task|Glob|WebFetch|WebSearch` dazu. Damit sieht der Hook den Start eines Shadows, und die Bremse in (b) erfasst alle Werkzeuge, die Arbeit kosten. Bewusst nicht erfasst bleibt das Übergabe-Werkzeug des Subagenten (in dieser Umgebung `SubagentHandback`): Nur darüber erreicht der unvollständige Stand die Hauptsession, auch der hook_error-Pfad darf es nicht sperren. Ebenso nicht erfasst bleiben reine UI-Werkzeuge wie TodoWrite. Für Glob, WebFetch, WebSearch, Agent und Task greift heute keine Regel. Ein Test belegt, dass sie ohne Mana-Befund erlaubt bleiben.
- **Erkennen des Shadows:** Werkzeugname `Agent` oder `Task`. `tool_input.subagent_type` zählt nur mit dem Teil nach dem letzten `:` (wie `ToolCallPolicy.AgentName`). `tool_input.model` darf `opus`, `sonnet`, `haiku`, eine volle Modell-ID oder `inherit` sein. Die neuen Felder liest der Hook nachsichtig (`JsonElement`): Ein Wert, der kein Text ist, zählt als fehlend und löst keinen hook_error aus.
- **Modellklasse des Shadows:** `tool_input.model` (Familie aus dem Namen; `inherit` zählt als fehlend), sonst das Modell aus der Agent-Definition für die Umbrarch-Agenten (warden, kritiker, strategist: opus; knight, scout: sonnet; ein Test gleicht das mit `plugin/agents/*.md` ab), sonst das Modell der Session aus `mana.json`, sonst Opus (`ModelClasses.Of`).
- **Erwartete Kosten (5h, 7d):** warden/opus 18 und 3, knight/sonnet 8 und 1. Für jede andere Kombination gilt der konservative Default 18 und 3, der höchste belegte p90. Vorschlag: p90, und einen eigenen Wert nur ab n ≥ 5. Begründung mit dem Fall aus der Sammlung 6b (Noelia, 21.11.2026, `cli-20261121.e68aa931.jsonl` seq 5 um 00:49:04 mit 77 %, seq 6 um 01:11:40 mit 95 %): Der Warden startete bei 77 % (Grenze 90, Noelia `.monarch/system.yaml:8`). Mit dem Median (10) hätte der Hook ihn gestartet (87 %), er endete bei 95 %. Mit p90 (18) verweigert der Hook (95 %).
- **Plan:** Gemessen ist nur Plan Pro. Für Max und Ultra sind die Prozentwerte je Lauf kleiner (größeres Fenster); die Pro-Werte gelten dort als obere Schranke (Vorschlag). Beim API-Key gilt die Tabelle ebenso.
- **Entscheidung:** `ManaGuard.Decide(Snapshot, Kosten, Grenzen)` mit den Grenzen der Session-Modellklasse (`ManaSupport.LimitClass` wie bei `quest accept`, `QuestCommands.cs:74`; die `session_id` kommt aus der Hook-Eingabe, wie beim Briefing, `HookRunner.cs:435`). Alles außer Proceed wird ein Deny für den Agent-Aufruf mit Regel `mana_shadow_start`, mit dem Grund aus dem Guard („passt nicht“, „Reset in unter 10 Minuten“ oder „Reset erfolgt“, Abschnitt 3). Ohne Snapshot gibt es kein Deny, wie bei `quest accept` (Vorschlag).
- **Ort:** Beide Mana-Prüfungen laufen einmal je Aufruf im HookRunner, nicht je Projekt in `ToolCallPolicy`. Ihr Ergebnis wird mit den Policy-Ergebnissen kombiniert (Deny vor Ask vor Allow) und einmal im Ledger des nächsten Projekts (`primary`) protokolliert.
- **Ledger:** Jeder Agent-Aufruf schreibt `ShadowStarted` (agent_type, model_class, est_5h, est_7d, five_hour_used_pct, stale, decision). Eine `agent_id` gibt es vor dem Start noch nicht; die nächste Messung ordnet `ShadowStarted` dem folgenden Lauf desselben Typs zu.
- **Grenze:** Laufen schon andere Shadows (parallel oder im Hintergrund), zählt der Hook deren restliche Kosten nicht dazu. Er sieht nur den Stand, den sie bis dahin verbraucht haben.
- **Nicht in diesem Gate (Vorschlag):** laufende Neuberechnung der Tabelle aus neuen Läufen. Die Tabelle steht fest im Code und verweist auf diesen ADR. Eine neue Messung mit `tools/shadow-costs.py` ersetzt sie (Offen 3).

### (b) Bremse im laufenden Shadow

- Jeder PreToolUse-Aufruf mit nicht leerer `agent_id` liest den Snapshot. Deny mit Regel `mana_brake`, wenn für ein Fenster, das laut Snapshot noch nicht zurückgesetzt ist (`resets_at` > jetzt), gilt: Stand ≥ Grenze (5h: `Effective5h`, 7d: `Effective7d`). Grenzen der Session-Modellklasse wie in (a). Grund: „Mana-Grenze erreicht (5h x %, Grenze y %). Stoppe und melde deinen Stand als unvollständig an die Hauptsession.“
- Vorschlag (Lesart von „ab der Grenze“): kein Schrittaufschlag, der Vergleich ist „Stand ≥ Grenze“.
- Vorschlag: Ein Wert aus einem schon zurückgesetzten Fenster zählt nicht, weil er den alten Verbrauch zeigt. Ohne Snapshot gibt es kein Deny, wie bei `quest accept`.
- Die Hauptsession bremst der Guard wie bisher über `mana --estimate`. Die Bremse gilt nur für Shadows.
- **Grenzen:** Ohne Statusline (headless, `claude -p`) wird `mana.json` nicht frisch; dann bremst nur der letzte bekannte Wert.
- **Latenz:** Pro Shadow-Werkzeug kommt ein Lesen von `mana.json` dazu (eine kleine Datei). Die Szenarien des Latenz-Gates laufen ohne `agent_id` und ändern sich nicht. Ein Szenario mit `agent_id` fehlt dem Latenz-Gate; es kommt ins Issue „Nacharbeit G-0130“.

## 3. „Reset erfolgt, Stand wird erneuert“

- **Heute** liefert `ManaGuard.Decide` für ein schon zurückgesetztes Fenster PauseWithoutRecap, nicht RecapAndStop: `reset - clock` ist negativ und damit unter 10 Minuten (`ManaPolicy.cs:42-43`, Testkommentar `tests/Monarch.Cli.Tests/ManaLimitTests.cs:170`). Die Vorgabe des Owners („statt RecapAndStop“) geht von RecapAndStop aus. Das Design ersetzt den heutigen Fall, egal wie er heißt.
- **Neue Reihenfolge in `Decide`:**
  1. Der Schritt passt in jedes vorhandene Fenster: Proceed.
  2. Ein Fenster, das nicht passt, ist laut Snapshot zurückgesetzt (`resets_at` ≤ jetzt): Seit dem Reset sind weniger als 10 Minuten vergangen, dann neuer Fall `ResetPending`. Sonst RecapAndStop mit dem bestehenden Hinweis „Stand veraltet, interaktive Session öffnen“ (`QuestCommands.cs:81`, #87).
  3. Der Reset des maßgeblichen Fensters (5h, ohne 5h die Woche) liegt in der Zukunft und weniger als 10 Minuten entfernt (`0 < resets_at − jetzt < 10 Minuten`): PauseWithoutRecap. Ein schon vorbeigegangener Reset des maßgeblichen Fensters fällt nicht unter Schritt 3, sondern unter Schritt 4.
  4. Sonst RecapAndStop.
- Vorschlag für die Obergrenze: `ManaStore.StaleAfter` (10 Minuten, ADR-0004), keine neue Zahl. Die Vorgabe des Owners nennt keinen Wert. Vorschlag für die Zeit danach: RecapAndStop statt heute PauseWithoutRecap. Das ist eine Verhaltensänderung: Kommt nach 10 Minuten kein frischer Stand (etwa ohne Statusline), sichert die Session ihren Stand im Recap, statt ohne Recap zu pausieren.
- Die Meldung ist überall gleich. Der erste Satz ist der Wortlaut des Owners, der Rest ist ein Vorschlag: „Reset erfolgt, Stand wird erneuert. Erneut prüfen ab HH:MM UTC (in 1 Minute), letzte Prüfung bis HH:MM UTC.“ Sie steht in `mana --estimate`, in der Ablehnung von `quest accept`, in `status`, in `mana_forecast` und im Hook vor einem Shadow-Start.
- Die Session wartet eine Minute und prüft erneut, bis Proceed kommt oder die Obergrenze erreicht ist. Wie sie wartet, regelt der Mana-Skill (Q-0198).
- `WakePlan.Slack` wird 1 Minute statt 3. Die Nachfrist des Außen-Jobs (3 Minuten) bleibt. Liegt `resets_at` beim Aufruf von `monarch rest` schon zurück, ist die Weckzeit jetzt + 1 Minute statt einer Zeit in der Vergangenheit (Vorschlag). `WakePlan.ResetsAt(wake) = wake − Slack` bleibt eine Näherung; mit dem Ersatzfall und den Jitter-Minuten trifft sie den Reset nicht genau, das ändert nichts an der Zeitprüfung. Vorschlag: Auch die Regel gegen die lokalen Minuten :00 und :30 bleibt (ADR-0008, Jitter von CronCreate). Bei einem Reset auf :59 oder :29 rückt der Weckruf damit eine Minute weiter, auf Reset + 2 Minuten.

## 4. Status nach dem Merge

**Befund.** Nach dem Squash liegt der Monarch-Stand (Quests `cleared`, `GateMerged`) nur im Aufräum-Commit auf dem Gate-Branch (#139). Der Gate-Status bleibt immer `proposed`, kein Befehl ändert ihn. develop verlangt grüne Pflicht-Checks (GitHub-Branchschutz, `required_status_checks`). Ein Commit mit `[skip ci]` direkt auf develop würde abgewiesen. Ein PR nur für den Status bräuchte CI, Verdict und Merge.

**Design (Vorschlag).** Es erfüllt Vorgabe 4 mit Verzögerung: Der Stand nach dem Merge kommt mit dem nächsten Gate auf develop, nicht sofort (Offen 2).
- `gate merge` setzt nach `GateMerged` den Gate-Status in der Worktree auf `merged`. Er kommt mit dem Aufräum-Commit auf den Gate-Branch. `merged` kommt in die geschlossenen Zustände (`src/Monarch.Cli/GateMergeCommands.cs:20`), damit ein zweites `gate merge` und ein spätes `gate verdict` das Gate als nicht offen abweisen (`Resolve`, `:669`, aufgerufen in `:70` und `:374`). `gate check` liest `GateMerged` aus dem Ledger (`:118`) und ist davon nicht betroffen.
- Neuer Befehl `monarch gate carry` (nur Main), in der neuen Gate-Worktree. Er sucht unter `origin/monarch/G-*` die Gates, deren Gate-Datei am Branch-Ende `state: merged` zeigt, während die Gate-Datei in der Worktree einen anderen Status hat („älter“). Je solches Gate übernimmt er nur das Feld `state`: in der Gate-Datei und in den Quest-Dateien, die in der Worktree zu diesem Gate gehören (`gate:` gleich, in der Quest-Liste des Gates). In die Gate-Datei übernimmt `carry` nur den Wert `merged`, denn der Gate-Status ist freier Text (`BoardGate.State` ist ein `string`, `src/Monarch.Application/Ports/IQuestBoard.cs:12`) und eine Liste gültiger Werte gibt es nicht, nur die geschlossenen Zustände in `src/Monarch.Cli/GateMergeCommands.cs:20`. In Quest-Dateien übernimmt es nur einen gültigen Quest-Zustand (`src/Monarch.Infrastructure.Boards/BoardNames.cs:8-19`). Alle anderen Felder und alle anderen Dateien bleiben unverändert.
- Ledger-Dateien übernimmt `carry` nicht. Sie gehören je zu einer Worktree (Writer-ID im Dateinamen, `JsonlLedgerStore.cs:9`). In einer neuen Worktree wären sie fremd, und ein normaler Commit damit wäre nach ADR-0018 §3 ein `UnanchoredCommit` (`LedgerHistory.cs:166-176`), den `ledger verify`, `gate merge`, `quest clear` und `evidence sync` verweigern. Die Vorgabe nennt nur Gate- und Quest-Dateien. Der Hinweis „no GateMerged“ von `evidence sync` (#139) bleibt deshalb bestehen.
- Vertrauen: Der Gate-Branch auf origin ist kein vertrauenswürdiger Stand (ADR-0018 §2). Deshalb übernimmt `carry` nur Statuswerte, und nur für Gates, die in der Worktree schon existieren. Ereignis `GateStatusCarried` (gate, quests, from_ref). Wiederholt ist der Befehl ohne Wirkung. Die Session committet das Ergebnis mit ihrem ersten Commit; develop bekommt den Stand mit dem nächsten Merge.
- Folge: Zwischen zwei Gates zeigt develop den alten Stand. Die Alternative ist ein Status-Commit direkt auf develop, und der braucht eine Ausnahme im Branchschutz (Offen 2).

## 5. gate move-quest

- `monarch gate move-quest <Q> --to <G>` (nur Main), in der Worktree des Gates. Vorschlag für die Ablehnungsgründe: Ablehnung, wenn die Quest oder das Ziel-Gate fehlt, Quelle und Ziel gleich sind, die Quest aktiv ist (`active.json`) oder einen anderen Status als `proposed` oder `accepted` hat.
- Ein Schritt: alte Gate-Liste ohne Q, neue Gate-Liste mit Q, `gate:` in der Quest-Datei, dann `QuestMoved` (quest, from, to). Geschrieben wird atomar je Datei, in der Reihenfolge Ziel-Gate, Quelle, Quest. Jeder Schritt setzt einen Zielzustand. Bricht der Befehl ab, stellt ein zweiter Aufruf den Rest her. Steht alles schon am Ziel, meldet er „schon verschoben“ und schreibt kein Ereignis.
- `gate_plan`: Steht eine geplante Quest schon in einem anderen Gate (ersetzbar ist nur `proposed`), wird sie aus dessen Liste entfernt und `QuestMoved` geschrieben.

## 6. gate cleanup

- `monarch gate cleanup <G>` (nur Main), aus dem Haupt-Checkout nach ExitWorktree. Der Befehl findet die Worktree über `git worktree list --porcelain` am Branch `monarch/<G>-…`. Sie muss ein verknüpfter Worktree unter `.claude/worktrees/` sein.
- Ablehnung, wenn das cwd in der Worktree liegt (der Hook würde dort weiter Ledger-Zeilen schreiben), wenn `ledger verify` in der Worktree Befunde hat oder wenn für das Gate nach dem letzten `GateMerged` kein `IntegrationRunChecked` mit `green` vorliegt (Standardregel 2, Schritt 1). Ohne `GateMerged` kommt so nie ein `[skip ci]` vor dem Merge auf den Branch.
- Ablauf, alles per `git -C`: `GateCleanedUp` in die Worktree schreiben, `git -C <wt> add .monarch`, `git -C <wt> commit -m "<G>: Monarch-Stand nach Merge [skip ci]"` (der pre-commit-Hook läuft; schlägt er an, stoppt der Befehl und meldet den Fund), `git -C <wt> push origin monarch/<G>-…` (ausdrücklicher Refspec, nur der Gate-Branch), `git -C <haupt> worktree remove <wt>` ohne `--force`.
- Verweigert git das Entfernen, nennt Monarch Grund und Dateien (`git -C <wt> status --porcelain`) und endet mit Exit 1. Kein Text schlägt `--force` vor.
- Fehlerpfade (Vorschlag): Scheitert der Commit (etwa am pre-commit-Hook), wird nichts gepusht und nichts entfernt. Scheitert der Push, wird nichts entfernt. Ein zweiter Aufruf setzt fort: Ohne neue Änderungen entfällt der Commit, der Push ist ohne Wirkung, wenn origin schon auf dem Stand ist. `git add` nimmt nur `.monarch` (Vorschlag), weil nach dem Merge nur der Monarch-Stand offen sein darf; andere Änderungen lassen `worktree remove` scheitern, und Monarch nennt sie.

## Nur durch den Main

`gate cleanup`, `gate carry` und `gate move-quest` verweigert der Hook jedem Shadow, wie heute `gate merge|verdict|check` (`ProtectBranchesRule.cs:41-56`). Die Liste in `GateOnlyMain` bekommt die drei Unterbefehle dazu (Q-0197).
- Der Bash-Aufruf selbst läuft im Haupt-Checkout. Seine Hook-Zeilen landen dort (bestehende Lücke: untracked Ledger-Dateien im Haupt-Checkout).

## 7. Merge-Methode je Repo

Schon umgesetzt: `src/Monarch.Cli/MergeMethodResolver.cs:25-34` (Konfiguration `git.merge_method`, sonst Ableitung aus der Historie), ADR-0007 Nachtrag Q-0140, Issue #48 geschlossen. Keine Änderung.

## 8. Fremder Agent statt Shadow

Befund ohne Umbau: Der Hook behandelt einen `general-purpose`-Agenten wie jeden Shadow, aber nicht wie einen read-only Shadow. Scope-Lock, geschützte Pfade, Secrets, deny_bash und Rank gelten für jeden Aufrufer gleich (`ToolCallPolicy.cs:55-70`). Ask→Deny gilt für jeden Subagenten, also bei nicht leerer `agent_id` (`HookRunner.cs:23-38`, Bedingung in `:27`). `gate merge|verdict|check` und `wake schedule|cancel|fire` verweigert der Hook jedem Agenten (`ProtectBranchesRule.cs:41-56`, Bedingung `call.AgentType` nicht leer in `:20`). Die read-only-Regel greift nur für `warden` und `scout` (`ToolCallTypes.cs:104`, `read_only_shadows` in `.monarch/system.yaml`). Außer der read-only-Liste (`ToolCallPolicy.cs:455-462`) prüft keine Regel den Agenten beim Schreiben, und keine prüft das Modell. Ein fremder Agent schreibt also im Quest-Scope genauso wie ein Knight. Mit Abschnitt 2 zählt er mit dem Default 18 und 3.

## Belege

| Aussage | Datei:Zeile | Befehl |
|---|---|---|
| Hook-Matcher ohne Agent | `plugin/hooks/hooks.json:16` | `grep -n "matcher" plugin/hooks/hooks.json` |
| tool_input ohne subagent_type und model | `src/Monarch.Cli/Dtos.cs:35-47` | `grep -n "JsonPropertyName" src/Monarch.Cli/Dtos.cs` |
| Repo-Ermittlung für git-Befehle | `src/Monarch.Policy/ProtectBranchesRule.cs:205-245`, `src/Monarch.Policy/BranchGuard.cs:156-190` | `grep -n "FindRepos\|FindGitDir" src/Monarch.Policy/*.cs` |
| -C ersetzt die Basen; relative --git-dir/GIT_DIR gegen die Basen | `src/Monarch.Policy/ProtectBranchesRule.cs:223`, `:238` | `grep -n "bases = next\|AbsFrom(dir, b)" src/Monarch.Policy/ProtectBranchesRule.cs` |
| Gate-Status ist freier Text | `src/Monarch.Application/Ports/IQuestBoard.cs:12` | `grep -n "record BoardGate" src/Monarch.Application/Ports/IQuestBoard.cs` |
| Geschlossene Gate-Zustände und ihre Prüfung | `src/Monarch.Cli/GateMergeCommands.cs:20`, `:669`; Aufrufer von `Resolve` `:70`, `:374`; `gate check` liest `GateMerged` `:118` | `grep -n "ClosedGateStates\|Resolve(env, gateId" src/Monarch.Cli/GateMergeCommands.cs`, `grep -n 'Type == "GateMerged"' src/Monarch.Cli/GateMergeCommands.cs` |
| Gültige Quest-Zustände | `src/Monarch.Infrastructure.Boards/BoardNames.cs:8-19` | `grep -n "public static string State\|QuestState\.[A-Za-z]* =>" src/Monarch.Infrastructure.Boards/BoardNames.cs` (Kopf in :8, die acht Zustände in :10-17) |
| gate/wake nur Main, jeder Agent | `src/Monarch.Policy/ProtectBranchesRule.cs:20`, `:41-56` | `grep -n "GateOnlyMain" src/Monarch.Policy/ProtectBranchesRule.cs` |
| read-only nur warden, scout | `src/Monarch.Policy/ToolCallTypes.cs:104` | `grep -n "ReadOnlyShadowTypes { get" src/Monarch.Policy/ToolCallTypes.cs` |
| Ask→Deny für jeden Subagenten | `src/Monarch.Cli/HookRunner.cs:23-38` | `grep -n "ConvertAskForSubagent" src/Monarch.Cli/HookRunner.cs` |
| Alle Regeln für jeden Aufrufer | `src/Monarch.Policy/ToolCallPolicy.cs:55-70` | `grep -n "public static PolicyDecision Evaluate" src/Monarch.Policy/ToolCallPolicy.cs` |
| Guard ohne Fall „Reset vorbei“ | `src/Monarch.Policy/ManaPolicy.cs:37-44` | `grep -n "Decide" src/Monarch.Policy/ManaPolicy.cs` |
| Obergrenze 10 Minuten | `src/Monarch.Cli/State.cs:58` | `grep -n "StaleAfter = " src/Monarch.Cli/State.cs` |
| Hinweis „Stand veraltet“ (#87) | `src/Monarch.Cli/QuestCommands.cs:81` | `grep -n "Der Stand ist veraltet" src/Monarch.Cli/QuestCommands.cs` |
| Weckzeit Reset + 3 min | `src/Monarch.Domain/WakePlan.cs:22` | `grep -n "Slack = " src/Monarch.Domain/WakePlan.cs` |
| gate_plan liest und schreibt nur das eigene Gate | `src/Monarch.Cli/McpTools.cs:323-345` | `grep -n "oldGate\|UpsertGateAsync" src/Monarch.Cli/McpTools.cs` |
| GateMerged erst nach dem Squash | `src/Monarch.Cli/GateMergeCommands.cs:312` | `grep -n '"GateMerged"' src/Monarch.Cli/GateMergeCommands.cs` |
| Gate-Status bleibt proposed | `.monarch/gates/G-0129-mana-grenze-arbeitsgrenze-je-plan-und-mo.md:6` auf develop: `state: proposed`; der Gate-Branch ändert diese Datei nach dem Merge nicht (nächste Zeile) | `grep -n "^state:" .monarch/gates/G-0129-*.md` |
| Nach dem Merge nur .monarch-Unterschiede | Ausgabe: 5 Dateien, 84 Einfügungen, 3 Löschungen, nur zwei Ledger-Dateien und drei Quest-Dateien (Q-0189 bis Q-0191) | `git diff --stat origin/develop origin/monarch/G-0129-mana-grenze-arbeitsgrenze` |
| develop mit Pflicht-Checks | Ausgabe: `required_status_checks` mit `build-test (ubuntu-latest)`, `plugin`, `aot (linux-x64)`, `enforcement_level: everyone` | `gh api repos/DavidOeztuerk/umbrarch/branches/develop --jq .protection` |
| Merge-Methode umgesetzt | `src/Monarch.Cli/MergeMethodResolver.cs:25-34` | `grep -n "public static MergeMethodChoice Resolve" src/Monarch.Cli/MergeMethodResolver.cs` |
| Issue #48 geschlossen | Ausgabe: `CLOSED` | `gh issue view 48 --json state -q .state` |
| Plan Pro | `.monarch/system.yaml:6` | `grep -n "plan:" .monarch/system.yaml` |
| Plan Pro seit 28.10.2026 | Ausgabe: `5c9f83e 2026-10-28 G-0101 …` | `git log --format="%h %ad %s" --date=short -S"plan:" -- .monarch/system.yaml` |
| Modelle der Agenten | `plugin/agents/*.md:4` | `grep -n "^model:" plugin/agents/*.md` |
| Modelle seit 28.10.2026 unverändert | Ausgabe: je ein Commit `a5b3cd2 2026-10-28` | `git log --format="%h %ad" --date=short -L4,4:plugin/agents/warden.md` (ebenso knight.md) |
| Messung der Shadow-Kosten | Strenge Tabelle (höchstens 2 min, ungerundet): Nachrechnung im PR-Kommentar `#issuecomment-7078646801`, Befund 3. Erste, gerundete Auswertung (28 Läufe): `clean.txt` im Scratchpad der Session, mit `collect_ledger.py`, `shadow_costs.py`, `clean_stats.py`. Bis Q-0194 die Logik nach `tools/shadow-costs.py` bringt | `python3 -I tools/shadow-costs.py ~/Projects/umbrarch ~/Projects/Noelia ~/Projects/workertransfer` (ab Q-0194) |
| Fall aus 6b (77 % → 95 %) | Noelia `.monarch/ledger/2026-11/cli-20261121.e68aa931.jsonl` seq 5, 6 (nicht im Arbeitsordner) | `git -C ~/Projects/Noelia log --all --oneline -- .monarch/ledger/2026-11/cli-20261121.e68aa931.jsonl`, dann `git -C ~/Projects/Noelia show <commit>:<pfad>` |
| Freshness im Shadow-Lauf | Noelia `.monarch/ledger/2026-11/be145c6d-54b5-441d-b8d7-f174b5fd2cfe.1ba0b664.jsonl` seq 179, 209 und `.monarch/ledger/2026-11/cli-20261121.1ba0b664.jsonl` seq 29–31, beide nicht im Arbeitsordner, aus den Branches gelesen | `git -C ~/Projects/Noelia log --all --oneline -- .monarch/ledger/2026-11/cli-20261121.1ba0b664.jsonl`, dann `git -C ~/Projects/Noelia show <commit>:<pfad>` für beide Dateien |

## Offen

1. Weitere schreibende Git-Befehle im Haupt-Checkout sperren (`add`, `restore`, `clean`, `cherry-pick`, `revert`, `am`, `apply`, `mv`, `rm`, `branch -f`, `tag`, `update-ref`)? Owner.
2. Status direkt nach dem Merge auf develop statt mit dem nächsten Gate: Das braucht eine Ausnahme im GitHub-Branchschutz von develop. Owner.
3. Laufende Neuberechnung der Shadow-Kosten aus neuen Läufen (Issue für v0.2). Vorschlag.
