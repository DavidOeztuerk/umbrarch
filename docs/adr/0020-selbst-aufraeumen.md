# ADR-0020: Selbst aufräumen (v0.2 Teil 1)

- Status: Vorgeschlagen (Warden-Designrunde ausstehend)
- Datum: 2026-11-22
- Gate: G-0130, Quests Q-0192 bis Q-0198
- Bezug: #46, #161, #74, #139, #16, #81, #44, #36, ADR-0004, ADR-0007, ADR-0008, ADR-0011, ADR-0012

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
3. **Nach dem Reset:** Zeigt der Stand ein Fenster, das schon zurückgesetzt ist, sagt Monarch „Reset erfolgt, Stand wird erneuert“. Die Session prüft dann jede Minute neu, höchstens 10 Minuten lang (Vorschlag). Der Weckruf kommt eine Minute nach dem Reset statt nach drei (selten zwei, wegen der Jitter-Minuten).
4. **Status auf develop:** develop nimmt keinen Commit ohne grüne Pflicht-Checks an. Der Stand nach dem Merge kommt darum erst mit dem nächsten Gate nach develop: Ein neuer Befehl holt ihn in die neue Worktree (Vorschlag, Offen 2).
5. **Quest verschieben:** Ein Befehl hängt eine Quest in ein anderes Gate um, in beiden Gate-Dateien und in der Quest-Datei, und schreibt das ins Ledger.
6. **Aufräumen:** Ein Befehl erledigt das Aufräumen nach dem Merge: committen, pushen, Worktree entfernen, ohne `--force`.

## 1. Regel protect_main_checkout

**Was zählt.** Ein Bash-Segment mit dem Wort `git` und einem dieser Unterbefehle: `checkout`, `switch`, `reset`, `merge`, `pull`, `commit`, `rebase`, `stash`. Den Unterbefehl bestimmt `BashAnalysis.GitSubcommand` wie bei `protect_branches`; globale Optionen wie `-c` oder `--no-pager` davor zählen nicht. `git stash list` und `git stash show` lesen nur und zählen nicht (Vorschlag: Lesart von „schreibende Git-Befehle“).

**Wo der Befehl läuft.** Die Verzeichnisse vor dem Segment (cwd, `cd`, `pushd`, `popd` aus dem cwd-Modell), danach jedes `-C <dir>` der Reihe nach. Grundlage ist die Ermittlung von `FindRepos` in `ProtectBranchesRule.cs:205-245`: cwd-Modell, `-C`, `--git-dir` im eigenen Segment und `GIT_DIR` aus jedem Segment, Repo je Basis über `BranchGuard.FindGitDir`. Erweiterung gegenüber `FindRepos` (Vorschlag): `--work-tree` und `GIT_WORK_TREE` zählen ebenfalls. `FindRepos` überspringt `--work-tree` (`:69`) und liest `GIT_WORK_TREE` nicht. Statt der Suche wird dann der genannte Arbeitsbaum geprüft.

**Was ein Haupt-Checkout ist.** Das gefundene Git-Verzeichnis heißt `.git`, ist ein Verzeichnis und hat keine Datei `commondir`, ist also kein verknüpfter Worktree. Der Ordner darüber enthält ein Verzeichnis `.monarch` (erwecktes Projekt). Für `--work-tree`: Der Arbeitsbaum enthält ein Verzeichnis `.git` und ein Verzeichnis `.monarch`. Ein Worktree unter `.claude/worktrees/` hat eine `.git`-Datei. Sein Git-Verzeichnis liegt unter `.git/worktrees/<name>` und zählt nie.

**Entscheidung.** Deny mit Regel `protect_main_checkout` und dem Weg: „Der Haupt-Checkout bleibt unverändert (Standardregel 2). Arbeite in der Gate-Worktree: `git worktree add .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop`, dann EnterWorktree.“ Das gilt für Main und Shadows gleich. Die Regel läuft in `Evaluate` und in `EvaluateProtection`, also auch, wenn der Haupt-Checkout nur Ziel eines Befehls ist, etwa `git -C ~/Projects/Noelia checkout main` aus einer anderen Session.

**Ausnahme.** `git worktree add` zählt nicht, weil `worktree` nicht in der Liste steht. Dasselbe gilt für die anderen `worktree`-Unterbefehle (`list`, `remove`, `prune`). `worktree remove` braucht das Aufräumen (Abschnitt 6).

**Was die Regel nicht prüft (Grenzen nach ADR-0011).** Git ohne das Wort `git` im Segment (Alias, Wrapper-Skript, `sh -c "…"`, `xargs git`) und Pfade aus Variablen oder `$(…)`, die das cwd-Modell nicht auflöst. Andere schreibende Git-Befehle (`add`, `restore`, `clean`, `cherry-pick`, `revert`, `am`, `apply`, `mv`, `rm`, `branch -f`, `tag`, `update-ref`) stehen nicht in der Liste des Owners und sind nicht gesperrt (Offen 1).

**Tests.** Q-0193 beginnt mit einem roten Test (Vorgabe „Roter Test zuerst“): `git checkout main` im Haupt-Checkout eines erweckten Testprojekts wird heute erlaubt.

**Folge für den Owner.** Befehle, die der Owner selbst im Terminal oder mit `!` ausführt, laufen nicht durch den Hook. `git pull` im Haupt-Checkout bleibt so ein Owner-Befehl.

## 2. Mana-Bremse für Shadows

### Messung

Quelle: alle Ledger-Zeilen aus umbrarch, Noelia und workertransfer, aus den Arbeitsordnern und allen Branches (lokal und `origin`), dedupliziert nach Datei, `seq` und Hash; 14 941 Ereignisse, Stand 22.11.2026. Alle drei Projekte stehen auf Plan Pro (`.monarch/system.yaml` Zeile 6; in umbrarch seit Commit `5c9f83e` vom 28.10.2026). Die Messung lief am 22.11.2026 mit Skripten aus dem Scratchpad der Session. Aus dem Repo lässt sie sich erst wiederholen, wenn Q-0194 dieselbe Logik mit genau diesen Kriterien nach `tools/shadow-costs.py` (nur lesend) bringt.

Das Modell eines Shadows steht nicht im Ledger. Die Zuordnung kommt aus den heutigen Agent-Definitionen (`plugin/agents/*.md` Zeile 4, unverändert seit Commit `a5b3cd2` vom 28.10.2026). Läufe mit anderem Modell per `model`-Parameter wären falsch zugeordnet (Annahme).

So rechnet das Skript:
- Ein Shadow-Lauf beginnt mit dem ersten `ToolUsed` oder `PolicyDecided` seiner `agent_id` und endet mit `ShadowRunCompleted`.
- Gemessen wird `fiveHourUsedPct` zwischen dem letzten frischen `ManaSnapshotTaken` vor dem Start und dem ersten frischen nach dem Ende, beide im selben 5h-Fenster.
- Ein Lauf zählt nur, wenn alle folgenden Bedingungen gelten:
  - Beide Abstände (Snapshot bis Start, Ende bis Snapshot) sind auf ganze Minuten gerundet höchstens 2, also unter 2,5 Minuten.
  - Kein anderer Shadow-Lauf desselben Projekts überlappt.
  - Kein anderer Lauf desselben Projekts hat dieselben Werte vorher, nachher und Differenz (Ersatz für „dasselbe Messpaar“).
  - Die 7d-Differenz ist nicht negativ, es lag also kein Wochen-Reset dazwischen.
- Ergebnis: 104 Läufe ohne Überlappung haben ein Messpaar, 28 davon zählen.

Varianten ändern die genutzten Werte nicht. Mit Snapshot-Identität statt Wertegleichheit zählt ein Warden-Lauf mehr (n=15, Median 13). Ohne den 7d-Filter zählt noch einer (n=16, Median 12,5). p90 21, Max 36 und 7d-p90 3 bleiben in beiden Fällen gleich. Bei den 28 Läufen gibt es auch keine Überlappung über Projekte hinweg.

| Shadow (Modell laut `plugin/agents/*.md`) | n | 5h Median | 5h p90 | 5h Max | 7d p90 |
|---|---|---|---|---|---|
| warden (opus) | 14 | 11,5 | 21 | 36 | 3 |
| knight (sonnet) | 11 | 5 | 12 | 19 | 1 |
| strategist (opus) | 1 | 20 | – | 20 | – |
| scout (sonnet) | 2 | 3,5 | – | 5 | – |

Für kritiker und fremde Agenten (`general-purpose` u. a.) gibt es keinen sauberen Lauf.

**(c) Wird der Stand im langen Lauf frisch?** Ja. Beleg (Noelia, 21.11.2026): Die Hauptsession benutzt um 21:13:24 zuletzt ein Werkzeug (Session-Ledger `be145c6d…`, seq 179), der Warden endet um 21:29:30 (seq 209). Dazwischen ruft der Warden selbst `monarch mana` auf: `cli-20261121`, seq 30 um 21:21:17 mit 47 % und seq 31 um 21:25:48 mit 50 %, beide `stale: false`; vor dem Start (seq 29, 21:13:20) waren es 37 %. Ohne Aktivität der Hauptsession kann ein frischer Wert nach mehr als 10 Minuten nur aus einer Statusline während des Shadow-Laufs stammen (`ManaStore.StaleAfter`, `State.cs:58`).

### (a) Prüfung vor dem Start

- Der PreToolUse-Matcher erfasst alle Werkzeuge (`*` statt der Liste in `plugin/hooks/hooks.json:16`), damit der Hook das Agent-Werkzeug sieht und die Bremse in (b) jedes weitere Werkzeug erfasst (Vorschlag). Folge: Auch Glob, TodoWrite, WebFetch und andere rufen den Hook auf. Für sie greift keine bisherige Regel, es kommt also nur der Prozessstart dazu. Der Hook liest `tool_input.subagent_type` und `tool_input.model`.
- **Modellklasse des Shadows:** `tool_input.model`, sonst das Modell aus der Agent-Definition für die Umbrarch-Agenten (warden, kritiker, strategist: opus; knight, scout: sonnet; ein Test gleicht das mit `plugin/agents/*.md` ab), sonst das Modell der Session aus `mana.json`, sonst Opus (`ModelClasses.Of`).
- **Erwartete Kosten (5h, 7d):** warden/opus 21 und 3, knight/sonnet 12 und 1. Für jede andere Kombination gilt der konservative Default 21 und 3, der höchste belegte p90. Vorschlag: p90, und einen eigenen Wert nur ab n ≥ 5. Begründung mit dem Fall aus der Sammlung 6b (Noelia, 21.11.2026, `cli-20261121.e68aa931.jsonl` seq 5 um 00:49:04 mit 77 %, seq 6 um 01:11:40 mit 95 %): Der Warden startete bei 77 % (Grenze 90, Noelia `.monarch/system.yaml:8`). Mit dem Median (11,5) hätte der Hook ihn gestartet (88,5 %), er endete bei 95 %. Mit p90 (21) verweigert der Hook (98 %).
- **Entscheidung:** `ManaGuard.Decide(Snapshot, Kosten, Grenzen)` mit den Grenzen der Session-Modellklasse (`ManaSupport.LimitClass`, wie bei `quest accept`). Alles außer Proceed wird ein Deny für den Agent-Aufruf, mit dem Grund aus dem Guard („passt nicht“, „Reset in unter 10 Minuten“ oder „Reset erfolgt“, Abschnitt 3). Ohne Snapshot gibt es kein Deny, wie bei `quest accept`.
- **Ledger:** Jeder Agent-Aufruf schreibt `ShadowStarted` (agent_type, model_class, est_5h, est_7d, five_hour_used_pct, stale, decision). Damit kennt die nächste Messung das Modell genau.
- **Nicht in diesem Gate (Vorschlag):** laufende Neuberechnung der Tabelle aus neuen Läufen. Die Tabelle steht fest im Code und verweist auf diesen ADR. Eine neue Messung mit `tools/shadow-costs.py` ersetzt sie (Offen 3).

### (b) Bremse im laufenden Shadow

- Jeder PreToolUse-Aufruf mit nicht leerer `agent_id` liest den Snapshot. Deny mit Regel `mana_brake`, wenn das 5h-Fenster noch nicht zurückgesetzt ist (`resets_at` > jetzt) und `Effective5h ≥ Grenze 5h` gilt, oder wenn `Effective7d ≥ Grenze 7d` gilt. Grund: „Mana-Grenze erreicht (5h x %, Grenze y %). Stoppe und melde deinen Stand als unvollständig an die Hauptsession.“
- Vorschlag (Lesart von „ab der Grenze“): kein Schrittaufschlag, der Vergleich ist „Stand ≥ Grenze“.
- Vorschlag: Ein Wert aus einem schon zurückgesetzten Fenster zählt nicht, weil er den alten Verbrauch zeigt. Ohne Snapshot gibt es kein Deny, wie bei `quest accept`.
- Die Hauptsession bremst der Guard wie bisher über `mana --estimate`. Die Bremse gilt nur für Shadows.
- **Grenzen:** Ohne Statusline (headless, `claude -p`) wird `mana.json` nicht frisch; dann bremst nur der letzte bekannte Wert.
- **Latenz:** Pro Shadow-Werkzeug kommt ein Lesen von `mana.json` dazu (eine kleine Datei). Die Szenarien des Latenz-Gates laufen ohne `agent_id` und ändern sich nicht. Die Warden-Runde prüft das.

## 3. „Reset erfolgt, Stand wird erneuert“

- Neuer Fall `ManaDecision.ResetPending`. Er gilt, wenn der Schritt nicht passt, das maßgebliche Fenster (5h, ohne 5h die Woche) laut Snapshot zurückgesetzt ist (`resets_at` ≤ jetzt) und seitdem weniger als 10 Minuten vergangen sind. Vorschlag für die Obergrenze: `ManaStore.StaleAfter` (10 Minuten, ADR-0004), keine neue Zahl. Die Vorgabe des Owners nennt keinen Wert.
- Danach gilt wieder RecapAndStop mit dem bestehenden Hinweis „Stand veraltet, interaktive Session öffnen“ (`QuestCommands.cs:81`, #87).
- Die Meldung ist überall gleich. Der erste Satz ist der Wortlaut des Owners, der Rest ist ein Vorschlag: „Reset erfolgt, Stand wird erneuert. Erneut prüfen ab HH:MM UTC (in 1 Minute), letzte Prüfung bis HH:MM UTC.“ Sie steht in `mana --estimate`, in der Ablehnung von `quest accept`, in `status`, in `mana_forecast` und im Hook vor einem Shadow-Start.
- Die Session wartet eine Minute und prüft erneut, bis Proceed kommt oder die Obergrenze erreicht ist. Wie sie wartet, regelt der Mana-Skill (Q-0198).
- `WakePlan.Slack` wird 1 Minute statt 3. Die Nachfrist des Außen-Jobs (3 Minuten) bleibt. Vorschlag: Auch die Regel gegen die lokalen Minuten :00 und :30 bleibt (ADR-0008, Jitter von CronCreate). Bei einem Reset auf :59 oder :29 rückt der Weckruf damit eine Minute weiter, auf Reset + 2 Minuten.

## 4. Status nach dem Merge

**Befund.** Nach dem Squash liegt der Monarch-Stand (Quests `cleared`, `GateMerged`) nur im Aufräum-Commit auf dem Gate-Branch (#139). Der Gate-Status bleibt immer `proposed`, kein Befehl ändert ihn. develop verlangt grüne Pflicht-Checks (GitHub-Branchschutz, `required_status_checks`). Ein Commit mit `[skip ci]` direkt auf develop würde abgewiesen. Ein PR nur für den Status bräuchte CI, Verdict und Merge.

**Design (Vorschlag).** Es erfüllt Vorgabe 4 mit Verzögerung: Der Stand nach dem Merge kommt mit dem nächsten Gate auf develop, nicht sofort (Offen 2).
- `gate merge` setzt nach `GateMerged` den Gate-Status in der Worktree auf `merged`. Er kommt mit dem Aufräum-Commit auf den Gate-Branch.
- Neuer Befehl `monarch gate carry` (nur Main), in der neuen Gate-Worktree. Er sucht unter `origin/monarch/G-*` die Gates, deren Gate-Datei am Branch-Ende `merged` zeigt und deren Stand in der Worktree älter ist. Je solches Gate übernimmt er die Gate-Datei, die Quest-Dateien dieses Gates und die Ledger-Dateien vom Branch-Ende. Ledger-Dateien übernimmt er nur, wenn sie in der Worktree fehlen oder die Worktree-Fassung ein Anfang der Branch-Fassung ist. Sonst überspringt er die Datei und nennt sie. Ereignis `GateStatusCarried` (gate, files, skipped). Die Session committet das mit ihrem ersten Commit; develop bekommt den Stand mit dem nächsten Merge.
- Folge: Zwischen zwei Gates zeigt develop den alten Stand. Die Alternative ist ein Status-Commit direkt auf develop, und der braucht eine Ausnahme im Branchschutz (Offen 2).

## 5. gate move-quest

- `monarch gate move-quest <Q> --to <G>` (nur Main). Vorschlag für die Ablehnungsgründe: Ablehnung, wenn die Quest oder das Ziel-Gate fehlt, Quelle und Ziel gleich sind, die Quest aktiv ist (`active.json`) oder einen PR hat (Status `in_review`, `verified`, `cleared`).
- Ein Schritt: alte Gate-Liste ohne Q, neue Gate-Liste mit Q, `gate:` in der Quest-Datei, dann `QuestMoved` (quest, from, to). Geschrieben wird atomar je Datei, in der Reihenfolge Ziel-Gate, Quelle, Quest.
- `gate_plan`: Steht eine geplante Quest schon in einem anderen Gate (ersetzbar ist nur `proposed`), wird sie aus dessen Liste entfernt und `QuestMoved` geschrieben.

## 6. gate cleanup

- `monarch gate cleanup <G>` (nur Main), aus dem Haupt-Checkout nach ExitWorktree. Der Befehl findet die Worktree über `git worktree list --porcelain` am Branch `monarch/<G>-…`. Sie muss ein verknüpfter Worktree unter `.claude/worktrees/` sein.
- Ablehnung, wenn das cwd in der Worktree liegt (der Hook würde dort weiter Ledger-Zeilen schreiben), wenn `ledger verify` in der Worktree Befunde hat oder wenn für das Gate nach dem letzten `GateMerged` kein `IntegrationRunChecked` mit `green` vorliegt (Standardregel 2, Schritt 1). Ohne `GateMerged` kommt so nie ein `[skip ci]` vor dem Merge auf den Branch.
- Ablauf, alles per `git -C`: `GateCleanedUp` in die Worktree schreiben, `git -C <wt> add .monarch`, `git -C <wt> commit -m "<G>: Monarch-Stand nach Merge [skip ci]"` (der pre-commit-Hook läuft; schlägt er an, stoppt der Befehl und meldet den Fund), `git -C <wt> push origin monarch/<G>-…` (ausdrücklicher Refspec, nur der Gate-Branch), `git -C <haupt> worktree remove <wt>` ohne `--force`.
- Verweigert git das Entfernen, nennt Monarch Grund und Dateien (`git -C <wt> status --porcelain`) und endet mit Exit 1. Kein Text schlägt `--force` vor.
- Der Bash-Aufruf selbst läuft im Haupt-Checkout. Seine Hook-Zeilen landen dort (bestehende Lücke: untracked Ledger-Dateien im Haupt-Checkout).

## 7. Merge-Methode je Repo

Schon umgesetzt: `src/Monarch.Cli/MergeMethodResolver.cs:24-33` (Konfiguration `git.merge_method`, sonst Ableitung aus der Historie), ADR-0007 Nachtrag Q-0140, Issue #48 geschlossen. Keine Änderung.

## 8. Fremder Agent statt Shadow

Befund ohne Umbau: Der Hook behandelt einen `general-purpose`-Agenten wie jeden Shadow, aber nicht wie einen read-only Shadow. Scope-Lock, geschützte Pfade, Secrets, deny_bash und Rank gelten für jeden Aufrufer gleich (`ToolCallPolicy.cs:55-70`). Ask→Deny gilt für jeden Subagenten, also bei nicht leerer `agent_id` (`HookRunner.cs:23-38`, Bedingung in `:27`). `gate merge|verdict|check` und `wake schedule|cancel|fire` verweigert der Hook jedem Agenten (`ProtectBranchesRule.cs:41-56`, Bedingung `call.AgentType` nicht leer in `:20`). Die read-only-Regel greift nur für `warden` und `scout` (`ToolCallTypes.cs:104`, `read_only_shadows` in `.monarch/system.yaml`). Außer der read-only-Liste (`ToolCallPolicy.cs:455-462`) prüft keine Regel den Agenten beim Schreiben, und keine prüft das Modell. Ein fremder Agent schreibt also im Quest-Scope genauso wie ein Knight. Mit Abschnitt 2 zählt er mit dem Default 21 und 3.

## Belege

| Aussage | Datei:Zeile | Befehl |
|---|---|---|
| Hook-Matcher ohne Agent | `plugin/hooks/hooks.json:16` | `grep -n "matcher" plugin/hooks/hooks.json` |
| tool_input ohne subagent_type und model | `src/Monarch.Cli/Dtos.cs:35-47` | `grep -n "JsonPropertyName" src/Monarch.Cli/Dtos.cs` |
| Repo-Ermittlung für git-Befehle | `src/Monarch.Policy/ProtectBranchesRule.cs:205-245`, `src/Monarch.Policy/BranchGuard.cs:156-190` | `grep -n "FindRepos\|FindGitDir" src/Monarch.Policy/*.cs` |
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
| Merge-Methode umgesetzt | `src/Monarch.Cli/MergeMethodResolver.cs:24-33` | `grep -n "public static MergeMethodChoice Resolve" src/Monarch.Cli/MergeMethodResolver.cs` |
| Issue #48 geschlossen | Ausgabe: `CLOSED` | `gh issue view 48 --json state -q .state` |
| Plan Pro | `.monarch/system.yaml:6` | `grep -n "plan:" .monarch/system.yaml` |
| Plan Pro seit 28.10.2026 | Ausgabe: `5c9f83e 2026-10-28 G-0101 …` | `git log --format="%h %ad %s" --date=short -S"plan:" -- .monarch/system.yaml` |
| Modelle der Agenten | `plugin/agents/*.md:4` | `grep -n "^model:" plugin/agents/*.md` |
| Modelle seit 28.10.2026 unverändert | Ausgabe: je ein Commit `a5b3cd2 2026-10-28` | `git log --format="%h %ad" --date=short -L4,4:plugin/agents/warden.md` (ebenso knight.md) |
| Messung der Shadow-Kosten | Skripte und Ergebnisdateien im Scratchpad der Session (`collect_ledger.py`, `shadow_costs.py`, `clean_stats.py`, `clean.txt`, `runs_noovl.txt`), bis Q-0194 sie nach `tools/shadow-costs.py` bringt | `python3 -I tools/shadow-costs.py ~/Projects/umbrarch ~/Projects/Noelia ~/Projects/workertransfer` (ab Q-0194) |
| Fall aus 6b (77 % → 95 %) | Noelia `.monarch/ledger/2026-11/cli-20261121.e68aa931.jsonl` seq 5, 6 (nicht im Arbeitsordner) | `git -C ~/Projects/Noelia log --all --oneline -- .monarch/ledger/2026-11/cli-20261121.e68aa931.jsonl`, dann `git -C ~/Projects/Noelia show <commit>:<pfad>` |
| Freshness im Shadow-Lauf | Noelia `.monarch/ledger/2026-11/be145c6d-54b5-441d-b8d7-f174b5fd2cfe.1ba0b664.jsonl` seq 179, 209 und `.monarch/ledger/2026-11/cli-20261121.1ba0b664.jsonl` seq 29–31, beide nicht im Arbeitsordner, aus den Branches gelesen | `git -C ~/Projects/Noelia log --all --oneline -- .monarch/ledger/2026-11/cli-20261121.1ba0b664.jsonl`, dann `git -C ~/Projects/Noelia show <commit>:<pfad>` für beide Dateien |

## Offen

1. Weitere schreibende Git-Befehle im Haupt-Checkout sperren (`add`, `restore`, `clean`, `cherry-pick`, `revert`, `am`, `apply`, `mv`, `rm`, `branch -f`, `tag`, `update-ref`)? Owner.
2. Status direkt nach dem Merge auf develop statt mit dem nächsten Gate: Das braucht eine Ausnahme im GitHub-Branchschutz von develop. Owner.
3. Laufende Neuberechnung der Shadow-Kosten aus neuen Läufen (Issue für v0.2). Vorschlag.
