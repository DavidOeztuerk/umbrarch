# ADR-0021: develop grün belastbar, gate open, Plugin-Version (G-0131)

- Status: Entwurf mit Owner-Entscheiden vom 26.11.2026 (unten). Weder vom Owner noch vom Warden freigegeben.
- Datum: 2026-11-26
- Gate: G-0131
- Bezug: #167, #166, ADR-0007 (Merge-Policy, CI-Regel B2), ADR-0019 (Version), ADR-0020 §1 „Gate öffnen mit Sandbox“ und §6 `gate cleanup`

## Owner-Vorgaben

Wörtlich aus der Prompt des Owners vom 26.11.2026:

> 1. #167: "develop grün" für gate merge belastbar machen. evidence.required = die Pflicht-Checks der Branch-Protection; Nachtläufe (event schedule) zählen nicht; Platzhalter-/skipped-Checks aus älteren Läufen dürfen nicht blockieren; Umgang mit macOS-Runner-Engpass im Nachtlauf (timeout-minutes o. ä.) prüfen. Rote Tests zuerst.
> 2. #166: monarch gate open <G> <slug> als eingebauter Befehl, der genau das tut, was meine Shell-Funktion gate-open tut (fetch, worktree add --no-track von origin/develop, Ausgabe des Pfads); läuft im eigenen Terminal des Owners, nicht aus der Sandbox. Die Frage "worktree remove aus der Sandbox" aus #166 mit prüfen. Rote Tests zuerst.
> 3. Klein dazu: Die Plugin-Version wird bei Änderungen unter plugin/ nicht hochgezählt, darum erneuert /plugin install die Cache-Kopie nicht (heute nur per uninstall + install gelöst). Vorschlag, wie das künftig automatisch greift (z. B. Versionspflicht per Test/CI), als Punkt im Gate oder als Issue.
>
> Vorher: Design kurz im ADR (neu oder Nachtrag), mir vorlegen, bevor Code entsteht. Nicht mergen ohne meine Freigabe.

Owner-Entscheide zum ersten Entwurf, wörtlich aus dem Chat vom 26.11.2026:

> 1. R3 habe ich in .monarch/system.yaml der Worktree eingetragen (evidence.required mit den drei Pflicht-Checks, Zeile 30–31). Prüfe die Datei lesend und committe sie mit Q-0216.
> 2. #167: R3 + R1 wie vorgeschlagen. R2 streichen: Mit R3 und R1 bleibt dafür kein belegter Fall; weniger Logik. Falls du einen konkreten Fall belegen kannst, in dem R2 nach R1+R3 noch nötig ist, leg ihn vor, statt R2 zu bauen.
> 3. Versionspflicht in diesem Gate (Q-0218), Version 0.1.3.
> 4. macOS-Nachtlauf und ci.yml unverändert.
> 5. Selbst planen per gate_plan, kein Strategist.
> 6. Den jq-Deny auf ~/.claude/plugins/*.json als Fehlalarm-Issue am Ende des Gates.

Owner-Antworten auf die Fragen unter „Offen“, wörtlich aus dem Chat vom 26.11.2026:

> 1. „ci.yml unverändert“ meinte nur macOS und Nachtlauf. Der neue Versions-Schritt im Job plugin ist erlaubt (Q-0218).
> 2. R3 für alle Aufrufer: so lassen. Aber belege per gh api repos/DavidOeztuerk/umbrarch/branches/main/protection/required_status_checks, ob main die macOS-Checks verlangt. Wenn nicht, ist die Aussage „macOS-Fehler fällt beim PR nach main auf“ im ADR falsch: offen als Lücke benennen, nichts bauen.
> 3. Fix-PR-Regel bei rotem develop: nicht bauen, eigenes Issue am Ende des Gates.
> 4. Zusätzlich Issue: Deadline-Test in ForgeClearViewTests wackelt unter Last.

Alles Weitere ist technisches Design der Session. Wo es über den Wortlaut hinausgeht, steht „Vorschlag“. Die beantworteten Fragen stehen unter „Offen“ mit Verweis auf diese Antworten.

## Zusammenfassung (einfache Sprache)

1. **develop grün:** `gate merge` zählt künftig nur die drei Pflicht-Checks von develop. Checks aus Nachtläufen zählen nicht. Damit blockiert auch ein Platzhalter-Check aus einem alten Lauf nicht mehr, denn er ist kein Pflicht-Check. Am macOS-Nachtlauf ändert sich nichts, denn `timeout-minutes` hilft nicht gegen einen Job, der nie startet.
2. **gate open:** `monarch gate open <G> <slug>` holt `origin`, legt die Worktree unter `.claude/worktrees/<G>` an (Branch `monarch/<G>-<slug>`, `--no-track`, von `origin/develop`) und gibt nur deren Pfad aus. In einer Claude-Code-Session verweigert der Befehl (Vorschlag). Aus der Sandbox gehen weder `worktree add` noch `worktree remove` (Probe unten). `gate cleanup` bleibt darum im Terminal des Owners.
3. **Plugin-Version:** Ändert ein PR etwas unter `plugin/`, muss er die Version erhöhen. Das prüft ein CI-Schritt im Job `plugin` (erlaubt, Owner-Antwort 1). Mit neuer Version legt `/plugin install` eine neue Cache-Kopie an.

## 1. develop grün (#167)

### Ursache (belegt)

Zeilenangaben in diesem Abschnitt meinen den Stand von develop 5681f44, vor der Änderung.

- `CheckCi` nimmt je Check-Name den neuesten Lauf, über alle Workflow-Läufe am Commit (`src/Monarch.Cli/EvidenceCommands.cs:161-162`). Ohne `evidence.required` zählen alle Namen (`:175-181`). Bis zum Eintrag des Owners (R3) stand in `.monarch/system.yaml` kein `evidence` (Stand develop 5681f44, Befehl in „Belege“).
- `GetChecksAsync` liest nur `…/commits/{sha}/check-runs`, ohne Workflow-Lauf und ohne Event (`src/Monarch.Infrastructure.Evidence/GitHubChecksEvidence.cs:27-60`). Monarch weiß darum nicht, welcher Check aus einem Nachtlauf stammt.
- Am develop-Head dfa5664 gab es den Namen `aot (${{ matrix.rid }})` nur im alten Nachtlauf 45406725392, als `skipped`. Er blieb also der neueste Stand dieses Namens, obwohl spätere Läufe am selben Commit grün waren. `gate merge` wertete das als rotes develop (`src/Monarch.Cli/GateMergeCommands.cs:451-469`, Text aus #167). Der Lauf ist gelöscht; die Job-Liste steht in #167.
- Pflicht-Checks von develop laut Branch-Protection: `build-test (ubuntu-latest)`, `plugin`, `aot (linux-x64)`, alle von GitHub Actions (`app_id` 15368).

### Regeln (neu)

In `CheckCi`, also für jeden Aufrufer: `quest verify`, `gate merge` (PR-Head, develop-Head und die Prüfung nach dem Merge, `GateMergeCommands.cs:576`), `gate check` und die Forge-Sicht von `quest list` und `status` (`ForgeClearView.cs:84`). Für R1 ist das ein Vorschlag (eine Regel für alle, wie ADR-0007 B2). Am PR-Head gibt es keine Nachtläufe: Geplante Läufe laufen auf dem Default-Branch, und der ist auf GitHub develop (`gh api … --jq .default_branch`; der Nachtlauf 45821416105 hat `headBranch` develop und `headSha` dfa56649, den damaligen develop-Head). In der Praxis betrifft R1 darum nur den develop-Head. Der Kommentar `.github/workflows/ci.yml:9` und `docs/adr/0015-relatives-latenz-gate.md:342` nahmen main als Default auf GitHub an (Nachtrag vom 18.11.2026); seit wann develop der Default ist, ist nicht belegt. Beides bleibt unverändert (Owner-Entscheid 4 für `ci.yml`). Das lokale `origin/HEAD` zeigt weiter auf main (`git symbolic-ref refs/remotes/origin/HEAD`); darauf beziehen sich `plugin/skills/quest/SKILL.md:15` und `plugin/skills/regeln/SKILL.md:46`.

- **R1, Nachtläufe zählen nicht (Owner):** Checks aus einem Workflow-Lauf mit `event: schedule` fallen weg, bevor gezählt wird. Das gilt auch, wenn der Lauf noch läuft.
- **R2 gestrichen (Owner-Entscheid 2).** Einen Fall, in dem R2 nach R1 und R3 noch nötig wäre, hat die Session nicht gefunden. Pro Name zählt der neueste Lauf; ein älteres `skipped` eines Pflicht-Namens überdeckt darum jeder neuere Lauf dieses Namens. Der Platzhalter `aot (${{ matrix.rid }})` ist kein Pflicht-Name. Die drei Pflicht-Checks entstehen in jedem Lauf, der kein Nachtlauf ist, solange `build-test` grün ist (`.github/workflows/ci.yml:40-45`: `build-test (ubuntu-latest)` und `aot (linux-x64)` in beiden Zweigen, `plugin` ohne Matrix). Ist `build-test` rot, fehlt `aot (linux-x64)`, denn `aot` hängt an `build-test` (`ci.yml:70`, `needs: [plan, build-test]`) und erscheint dann nur als Platzhalter. Auch das löst R2 nicht; dazu „Offen“ 3.
- **Mit R3 und rotem `build-test` auf develop (Befund des Kritikers, nicht gebaut):** `CheckCi` liefert Pending, sobald ein Pflicht-Check fehlt, bevor rote Checks gewertet werden (`EvidenceCommands.cs:167-171` am Stand 5681f44, gleiche Logik jetzt in `:173-177`). `gate merge` verweigert dann mit „develop läuft noch“ (`GateMergeCommands.cs:452-455`), und `gate check` meldet „pending“ statt „red“. Einen Fix-PR lässt `gate merge` in diesem Fall weder mit noch ohne R3 zu. Ohne R3 ist der Platzhalter `aot (${{ matrix.rid }})=skipped` selbst ein roter Check, und `FixPrProof` verweigert, weil er am PR-Head fehlt (`GateMergeCommands.cs:514-520`). Der Wächter-Test 9 hält das Verhalten mit R3 fest.
- **R3, `evidence.required` (Owner):** eingetragen vom Owner in `.monarch/system.yaml:30-31`:
  ```yaml
  evidence:
    required: ["build-test (ubuntu-latest)", "plugin", "aot (linux-x64)"]
  ```
  `system.yaml` ist ein geschützter Pfad (`.monarch/system.yaml:25`, `protect_paths`). Die Session committet die Datei mit Q-0216.
- Unverändert: Pro Name zählt der neueste Lauf, nur `success` ist grün. Fehlt ein Pflicht-Check, ist das Ergebnis Pending, und `gate merge` verweigert mit „develop läuft noch“ (`GateMergeCommands.cs:452-455`). Checks ohne Workflow-Lauf (andere Apps) lässt R1 unberührt; mit R3 zählen sie nur, wenn ihr Name in der Liste steht.

**Datenquelle (Vorschlag):** zusätzlich `GET /repos/{o}/{r}/actions/runs?head_sha={sha}` (mit Paging). Jeder Check-Run nennt seine `check_suite.id`, jeder Workflow-Lauf `check_suite_id` und `event`. Darüber bekommt jeder Check das Event seines Laufs. Die Regel selbst steht in `CheckCi`. Scheitert die Abfrage, verweigern `quest verify` und `gate merge` wie bei jedem Forge-Fehler (fail-closed).

**Folgen, offen benannt:**
- *Lockerung (Owner-Entscheid, R1):* Ein roter Nachtlauf hält develop nicht mehr für rot, wenn der Push-Lauf am selben Commit grün ist. Er bleibt in Actions sichtbar.
- *R3 gilt per Code für jeden Aufrufer.* `CheckCi` liest `evidence.required` schon heute für alle Aufrufer oben (`EvidenceCommands.cs:163-173` am Stand 5681f44). Mit dem Commit von `system.yaml` zählen darum auch bei `quest verify`, am PR-Head, bei `gate check` und in der Forge-Sicht nur die drei Namen. Owner-Entscheid 2 nennt #167, also `gate merge`; für den Rest gilt Owner-Antwort 2 („so lassen“, „Offen“ 2).
- *Lockerung (Owner-Entscheid, R3):* Checks außerhalb der drei Pflicht-Checks blockieren `gate merge` nicht mehr, etwa `build-test (macos-latest)` und `aot (osx-arm64)`. macOS läuft für develop nur im Nachtlauf und bei `workflow_dispatch` (`.github/workflows/ci.yml:40-42`). Die Branch-Protection von main verlangt `build-test (macos-latest)`, `build-test (ubuntu-latest)`, `plugin` und `aot (linux-x64)`, aber nicht `aot (osx-arm64)` (Belege). Ein Fehler in `build-test (macos-latest)` blockiert also erst den PR nach main. **Lücke (offen benannt, nichts gebaut; Lesart der Session: Owner-Antwort 2 gilt für den Teil, den main nicht verlangt):** Ein Fehler in `aot (osx-arm64)`, also im macOS-AOT-Build mit dem Latenz-Gate, blockiert weder `gate merge` noch den PR nach main.
- *Strenger als heute (R1):* Ist der Push-Lauf rot und ein späterer Nachtlauf am selben Commit grün, galt develop bisher als grün. Künftig bleibt es rot, bis der Push-Lauf wiederholt ist.

### macOS-Engpass im Nachtlauf

- `timeout-minutes` begrenzt die Laufzeit eines Jobs. Laut GitHub-Limits (`docs.github.com/en/actions/reference/limits`, wörtlich): „Each job in a workflow can run for up to 6 hours of execution time.“ Eine Grenze für die Wartezeit auf einen GitHub-hosted Runner nennt die Seite nicht; „Job queue time … 24 hours“ steht dort nur für self-hosted Runner. Die Laufzeit-Grenze greift also nicht für einen Job, der nie startet.
- Im Lauf 45406725392 stand `build-test (macos-latest)` auf `cancelled` (#167). Dass der Job nie gestartet ist, steht im Stand von G-0130. Nachprüfen lässt es sich nicht mehr, denn der Lauf ist gelöscht.
- Mit R1 hat der Nachtlauf keinen Einfluss mehr auf `gate merge`. Owner-Entscheid 4 (oben): macOS-Nachtlauf und `ci.yml` bleiben unverändert. `timeout-minutes` steht heute nirgends (`grep -n timeout-minutes .github/workflows/ci.yml`: keine Ausgabe).

### Tests

„Heute rot“ heißt: rot am Stand von develop (5681f44), grün nach der Änderung. „Wächter“ heißt: schon heute grün, hält das Verhalten fest.

- `tests/Monarch.Infrastructure.Tests/GitHubChecksEvidenceTests.cs`: Check-Runs tragen ihre Check-Suite; `actions/runs` liefert das Event je Suite, mit Paging. Heute rot (kompiliert nicht, die Methode fehlt).
- `tests/Monarch.Cli.Tests/DevelopGreenTests.cs`, über `gate merge`:
  1. **Boss, der Fall aus #167:** develop-Head mit altem Nachtlauf (Platzhalter `skipped`, macOS `cancelled`), neuerem grünem Nachtlauf und grünem Push-Lauf. `gate merge` merged. Ohne `evidence.required` (nur R1): heute rot; der rote Lauf zeigte dieselbe Meldung wie #167 („checks not green: aot (${{ matrix.rid }})=skipped“), der Test prüft den Text nicht. Mit R3: der Merge ist schon heute grün; rot ist heute nur die Prüfung, dass `actions/runs` gefragt wird.
  2. Ein roter Check nur aus einem Nachtlauf, Push-Lauf grün: merged. Heute rot.
  3. Push-Lauf rot, späterer Nachtlauf grün: verweigert („not green“). Heute rot (am alten Stand überdeckt der grüne Nachtlauf den roten Push-Lauf, der Merge geht durch).
  4. Mit R3: Pflicht-Check nur im Nachtlauf vorhanden: verweigert („develop läuft noch“, der Name steht in der Meldung). Heute rot.
  5. Ein noch wartender Job eines Nachtlaufs hält develop nicht als „läuft noch“ fest. Heute rot.
  6. Mit R3: ein roter Check außerhalb der Liste blockiert nicht, ein roter Pflicht-Check blockiert. Wächter.
  7. `actions/runs` antwortet mit 500: verweigert („merge failed“). Heute rot.
  8. `Forge.Required` liest aus der Fluss-Liste genau die drei Namen (direkter Aufruf, nicht über `gate merge`). Wächter.
  9. Mit R3 ist `build-test (ubuntu-latest)` rot und `aot (linux-x64)` fehlt (nur der Platzhalter): verweigert mit „develop läuft noch“, auch mit belegtem Fix-PR. Wächter für „Offen“ 3.
- `tests/Monarch.Cli.Tests/ForgeClearViewTests.cs:156`: Der Test zählt die Requests je PR. Mit R1 kommt `actions/runs` einmal je PR dazu; der Test prüft das ausdrücklich (3 statt 2).
- Der Fake in `tests/Monarch.Cli.Tests/Part2Support.cs` hat eine Route für `actions/runs`, im Default eine leere Liste. Ohne Eintrag zählt jeder Check wie heute.

## 2. gate open (#166)

### Verhalten

`~/.monarch/bin/monarch gate open <G-xxxx> <slug>`, gedacht für das eigene Terminal des Owners. Die Shell-Funktion (`~/.zshrc:27-33`) prüft, dass beide Argumente da sind (`:28`), setzt den Haupt-Checkout fest auf `~/Projects/umbrarch` und die Worktree auf `.claude/worktrees/$1` (`:29`), ruft `fetch origin` (`:30`) und `worktree add --no-track "$wt" -b "monarch/$1-$2" origin/develop` (`:31`) auf, wechselt dann in die Worktree und startet `claude` (`:32`). Was darüber hinausgeht, steht unten als Vorschlag.

1. Ist `CLAUDECODE` gesetzt, verweigert der Befehl, bevor er git aufruft (Vorschlag). Grund: In der Sandbox scheitert `worktree add` halb, und die Branch bliebe ohne Worktree zurück (Probe unten).
2. Prüft die Eingaben (Vorschlag): `G` passt auf `^G-\d{4,}$`, `slug` auf `^[a-z0-9]+(-[a-z0-9]+)*$` mit höchstens 60 Zeichen. So bleiben Pfad und Branch-Name ohne `..`, `/` und Leerzeichen.
3. Sucht den Haupt-Checkout (Vorschlag, statt des festen Pfads): erster Eintrag von `git worktree list --porcelain`, wie `gate cleanup` (`GateCleanupCommands.cs:35-47`). So geht der Befehl in jedem erweckten Projekt und auch aus einer Worktree.
4. `git -C <main> fetch origin`
5. `git -C <main> worktree add --no-track <main>/.claude/worktrees/<G> -b monarch/<G>-<slug> origin/<integration_branch>`. Der Branch kommt aus `system.yaml`, Default `develop` (Vorschlag; für umbrarch ist das `origin/develop`).
6. Ausgabe des Pfads (Owner). Vorschlag: stdout enthält nur den absoluten Pfad, damit die Shell hineinwechseln kann, und die Ausgabe von git geht nach stderr. Scheitert git (etwa weil Branch oder Pfad schon existieren), gibt Monarch den Text von git weiter und endet mit Exit 1, ohne Pfad.

Kein Ledger-Eintrag (Vorschlag). Der Befehl läuft im Haupt-Checkout, und eine Ledger-Zeile wäre dort eine untracked Datei. `gate carry` bleibt der erste Schritt in der neuen Session.

Die Shell-Funktion des Owners (`~/.zshrc:27-33`) könnte danach so aussehen (Vorschlag; die Datei ändert nur der Owner):

```sh
gate-open() {
  local wt
  wt=$(cd ~/Projects/umbrarch && ~/.monarch/bin/monarch gate open "$1" "$2") && cd "$wt" && claude
}
```

### Sandbox: worktree add und remove (Probe vom 26.11.2026)

Aus dieser Session (Gate-Worktree G-0131, Sandbox aktiv). Befehl in „Belege“: je Pfad `touch`, bei Erfolg sofort `rm`. Geprobt ist nur das Anlegen eines neuen Eintrags; eine fremde Worktree zum Entfernen gab es nicht (`git worktree list`: nur Haupt-Checkout und G-0131).

| Ziel | Ergebnis |
|---|---|
| `<main>/.git/worktrees/PROBE-G0131` (neuer Eintrag, wie bei `worktree add`) | gesperrt |
| `<main>/.claude/worktrees/PROBE-G0131` (neue Worktree) | gesperrt |
| `<main>/.git/worktrees/G-0131/PROBE-G0131` (eigene Worktree) | schreibbar, sofort entfernt |

Das passt zum Systemhinweis dieser Session: „`.git/worktrees` is read-only in the sandbox, apart from this session's own worktrees, so `git worktree add`, `move` and `remove` … fail there halfway.“

- **worktree add aus der Sandbox:** nein. Es braucht beide gesperrten Orte.
- **worktree remove aus der Sandbox:** nein für jede andere Worktree, laut Systemhinweis (nicht geprobt). Ihr Ordner liegt außerhalb der Schreiborte, wie die gesperrte Probe unter `.claude/worktrees/` zeigt. Die eigene Worktree darf sich nicht selbst entfernen: `gate cleanup` verweigert aus der Worktree heraus, weil der Hook dort weiter Ledger-Zeilen schreibt (`GateCleanupCommands.cs:69-73`).
- **Empfehlung (Vorschlag):** `gate cleanup` bleibt im Terminal des Owners. #166 Frage 2 ist damit beantwortet. Ein `allowWrite` auf `.claude/worktrees` und `.git/worktrees` würde jeder Session in jedem Projekt diese Ordner öffnen (ADR-0020 §1). Das schlägt die Session nicht vor.

Kurzantworten zu den übrigen Fragen aus #166: (3) `--no-track` bleibt, wie in der Shell-Funktion. (4) Entfällt, weil `gate open` außerhalb der Sandbox läuft. (5) Im Terminal des Owners ist `CLAUDECODE` leer, Schicht 2 lässt alles durch. (6) Die `denyWrite`-Einträge gelten nur in der Sandbox und stören das Terminal des Owners nicht.

### Tests (rot vor der Änderung)

`tests/Monarch.Cli.Tests/GateOpenTests.cs` mit einem temporären Repo, einem Bare-Remote und einer Worktree, wie `GateCleanupTests`:
- Legt `.claude/worktrees/<G>` auf Branch `monarch/<G>-<slug>` an. Der Commit kommt von `origin/develop`, nach dem Fetch: Das Remote ist dem lokalen `origin/develop` voraus, und die neue Worktree steht auf dem neuen Commit. Kein `branch.<b>.remote` in der Konfiguration. stdout ist genau der Pfad.
- Geht auch aus einer bestehenden Worktree; der Pfad liegt dann unter dem Haupt-Checkout.
- Mit `CLAUDECODE=1`: verweigert, keine Branch, keine Worktree.
- Ungültige Eingaben (`../x`, `G-1`, `Slug`, Leerzeichen): verweigert, git wird nicht aufgerufen.
- Branch existiert schon: Exit 1, der Text von git ist zu sehen, kein Pfad auf stdout.
- Kein erwecktes Projekt: verweigert (Vorschlag).
- Regeltext: Standardregel 2 nennt `~/.monarch/bin/monarch gate open <G-xxxx> <slug>` statt des git-Befehls (`plugin/skills/regeln/SKILL.md:10`, `:64`; `tests/Monarch.Cli.Tests/RegelnTextTests.cs:44`, Vorschlag).

## 3. Plugin-Version

### Ursache (belegt)

- Claude Code benennt den Cache nach der Version: „The version also names the plugin's cache directory.“ und „…keeps every user on the cached copy until its author changes the string, however many commits they push.“ (`code.claude.com/docs/en/plugins/loading`, „Versions and updates“). Auf dem Mac: `~/.claude/plugins/cache/umbrarch-local/umbrarch/` hat die Ordner `0.1.1` und `0.1.2`.
- G-0130 hat 12 Dateien unter `plugin/` geändert, die Version blieb 0.1.2 (Befehle in „Belege“).
- Die Version steht an sechs Stellen: `plugin/.claude-plugin/plugin.json:3`, `plugin/scripts/monarch:4` (daraus der Binary-Ordner `~/.monarch/bin/<version>`, `:65`), `src/Monarch.Cli/McpTools.cs:17` und fest in drei Tests: `plugin/tests/wrapper-test.sh:107` und `:113`, `tests/Monarch.Cli.Tests/VisibleLayerNameTests.cs:45`, `tests/Monarch.Cli.Tests/McpTests.cs:40`. Gleichheit prüfen heute der CI-Schritt `ci.yml:158-164` (Wrapper gegen `plugin.json`), `VisibleLayerNameTests.cs:40-46` (dasselbe, dazu fest 0.1.2, läuft in `dotnet test`, `ci.yml:65`) und `wrapper-test.sh:107` (fest 0.1.2, `ci.yml:152-153`). Ob die Version gegenüber der Basis gestiegen ist, prüft nichts. `scripts/install-local.sh:9`, `:25` installiert das Binary in den Ordner der Version aus `plugin.json`.

### Vorschlag

- **Skript `scripts/check-plugin-version.sh <basis>`:** Unterscheidet sich `plugin/` zwischen Basis und HEAD, muss die Version in `plugin.json` höher sein als in der Basis (Vergleich per `sort -V`). Außerdem müssen `plugin.json`, Wrapper und `McpTools.cs` dieselbe Version nennen. Sonst Exit 1 mit Text.
- **CI (erlaubt, Owner-Antwort 1):** Im Job `plugin`, nur bei `pull_request`, mit `origin/${{ github.base_ref }}` als Basis. Dafür braucht der Checkout `fetch-depth: 0`. Die CI ruft in `plugin/tests/` nur `wrapper-test.sh` beim Namen auf (`ci.yml:149-153`); ein neuer Test oder das Skript braucht also eine eigene Zeile in `ci.yml`.
- **Feste Versionen in Tests:** `wrapper-test.sh` liest die Version schon aus dem Wrapper (`:6`); fest stehen nur die Prüfungen `:107` und `:113`. Sie und `VisibleLayerNameTests.cs:45`, `McpTests.cs:40` lesen die Version künftig aus `plugin.json` oder dem Wrapper (Vorschlag).
- **Folge für den Owner:** Nach einem Gate mit neuer Version kommt zuerst `scripts/install-local.sh` (neuer Binary-Ordner), dann `/plugin install`. In umgekehrter Reihenfolge fragt der Wrapper bei jedem PreToolUse-Aufruf aus einem erweckten Projekt nach (fail-closed, `plugin/scripts/monarch:46`, ADR-0019, `wrapper-test.sh:106-113`); sonst bleibt er fail-open (`:50`).
- **Version:** Patch-Sprung je Gate mit Änderung unter `plugin/` (Vorschlag). Für dieses Gate 0.1.3 (Owner-Entscheid 3). Releases und Tags bleiben beim Owner.
- Nicht gewählt: `version` weglassen. Für einen Marketplace aus einem lokalen Pfad sagt die Doku, das Plugin lade „in place“. Auf dem Mac liegt trotzdem eine Kopie im Cache. Ohne Test ist unklar, was dann gilt, und `install-local.sh` braucht die Version.

### Tests (rot vor der Änderung)

`plugin/tests/plugin-version-test.sh` mit temporären Repos: Änderung unter `plugin/` ohne höhere Version → Exit 1; mit höherer Version → 0; ohne Änderung unter `plugin/` → 0; niedrigere Version → 1; Stellen ungleich → 1. Er läuft im CI-Job `plugin` (Owner-Antwort 1).

## Quests (Vorschlag)

| Quest | Inhalt | Rank | Art |
|---|---|---|---|
| Q-0216 | §1 develop grün (#167), dazu dieser ADR | A | heikel: ändert eine Merge-Schutzregel. Warden-Design-Runde vor dem Code |
| Q-0217 | §2 gate open (#166), Regeltext | B | normal: TDD |
| Q-0218 | §3 Versionspflicht, Version 0.1.3 | A (Risk-Tag infra, `ci.yml`) | normal: TDD, verschärft nur |

Am Ende eine Warden-Runde über den PR (Standardregel 3). Gemergt wird nicht ohne Freigabe des Owners. Ebenfalls am Ende drei Issues: der Fehlalarm zum Deny von `jq` auf `~/.claude/plugins/*.json` (Owner-Entscheid 6), die Fix-PR-Regel bei rotem develop (Owner-Antwort 3) und der unter Last wackelnde Deadline-Test in `ForgeClearViewTests` (Owner-Antwort 4).

## Belege

| Aussage | Datei:Zeile | Befehl |
|---|---|---|
| Neuester Lauf je Name, `required` grenzt ein | `src/Monarch.Cli/EvidenceCommands.cs:161-181` (Stand 5681f44) | `grep -n "GroupBy(c => c.Name)\|Forge.Required" src/Monarch.Cli/EvidenceCommands.cs` |
| `required` aus `system.yaml` | `src/Monarch.Cli/Support.cs:163-164` | `grep -n "Required(ProjectPaths" src/Monarch.Cli/Support.cs` |
| Kein `evidence` in `system.yaml` bis R3 | – | `git show 5681f44:.monarch/system.yaml \| grep -n "^evidence"` (keine Ausgabe, Exit 1) |
| R3 vom Owner eingetragen | `.monarch/system.yaml:30-31` | `grep -n "evidence\|required" .monarch/system.yaml` |
| `aot` hängt an `build-test` | `.github/workflows/ci.yml:70` | `grep -n "needs:" .github/workflows/ci.yml` |
| Fehlender Pflicht-Check vor rotem gewertet | `src/Monarch.Cli/EvidenceCommands.cs:167-171` am Stand 5681f44 | `git show 5681f44:src/Monarch.Cli/EvidenceCommands.cs \| grep -n "required checks missing"` |
| Lokales `origin/HEAD` | Ausgabe: `refs/remotes/origin/main` | `git symbolic-ref refs/remotes/origin/HEAD` |
| Nachtlauf auf develop | Ausgabe: `schedule develop dfa56649` | `gh run view 45821416105 --json event,headBranch,headSha` |
| `system.yaml` geschützt | `.monarch/system.yaml:25` | `grep -n "protect_paths" .monarch/system.yaml` |
| Check-Runs ohne Event | `src/Monarch.Infrastructure.Evidence/GitHubChecksEvidence.cs:27-60` (Stand 5681f44) | `grep -n "check-runs" src/Monarch.Infrastructure.Evidence/GitHubChecksEvidence.cs` |
| develop-Prüfung in `gate merge` | `src/Monarch.Cli/GateMergeCommands.cs:450-473` | `grep -n "integrationCi" src/Monarch.Cli/GateMergeCommands.cs` |
| Pflicht-Checks von develop | Ausgabe: `build-test (ubuntu-latest)`, `plugin`, `aot (linux-x64)`, `app_id` 15368 | `gh api repos/DavidOeztuerk/umbrarch/branches/develop/protection/required_status_checks` |
| Default-Branch develop | Ausgabe: `develop` | `gh api repos/DavidOeztuerk/umbrarch --jq .default_branch` |
| Nachtläufe auf develop, seit 20.11. am Head dfa5664 | Ausgabe: sechs `schedule`-Läufe, 45406725392 fehlt (gelöscht) | `gh run list --workflow ci --event schedule --limit 20` |
| Lauf 45406725392 gelöscht | Ausgabe: `HTTP 404` | `gh run view 45406725392` |
| Pflicht-Checks von main | Ausgabe: `build-test (macos-latest)`, `build-test (ubuntu-latest)`, `plugin`, `aot (linux-x64)` | `gh api repos/DavidOeztuerk/umbrarch/branches/main/protection/required_status_checks --jq .contexts` |
| macOS-Matrix nur bei schedule, dispatch, PR nach main | `.github/workflows/ci.yml:40-42` | `grep -n "macos-latest" .github/workflows/ci.yml` |
| Kein `timeout-minutes` | – | `grep -n "timeout-minutes" .github/workflows/ci.yml` (keine Ausgabe) |
| Shell-Funktion des Owners | `~/.zshrc:27-33` | `grep -n "gate-open" ~/.zshrc` |
| Probe Sandbox | Tabelle in §2 | `for p in /Users/davidozturk/Projects/umbrarch/.git/worktrees/PROBE-G0131 /Users/davidozturk/Projects/umbrarch/.claude/worktrees/PROBE-G0131 /Users/davidozturk/Projects/umbrarch/.git/worktrees/G-0131/PROBE-G0131; do if touch "$p" 2>/dev/null; then echo "WRITABLE $p"; rm -f "$p"; else echo "denied  $p"; fi; done` |
| Nur zwei Worktrees | Ausgabe: Haupt-Checkout (develop) und G-0131 | `git worktree list --porcelain` |
| `gate cleanup` verweigert aus der Worktree | `src/Monarch.Cli/GateCleanupCommands.cs:69-73` | `grep -n "not from inside the worktree" src/Monarch.Cli/GateCleanupCommands.cs` |
| Version an sechs Stellen | `plugin/.claude-plugin/plugin.json:3`, `plugin/scripts/monarch:4`, `src/Monarch.Cli/McpTools.cs:17`, `plugin/tests/wrapper-test.sh:107`, `:113`, `tests/Monarch.Cli.Tests/VisibleLayerNameTests.cs:45`, `tests/Monarch.Cli.Tests/McpTests.cs:40` | `grep -rn "0\.1\.2" plugin src scripts tests --include='*.json' --include='*.cs' --include='*.sh' --include=monarch` |
| G-0130 änderte `plugin/` ohne neue Version | Ausgabe: `12 files changed`; `"version": "0.1.2"` | `git diff --stat dfa5664 5681f44 -- plugin/`, `git show 5681f44:plugin/.claude-plugin/plugin.json` |
| Cache je Version | Ausgabe: `0.1.1`, `0.1.2` | `ls ~/.claude/plugins/cache/umbrarch-local/umbrarch/` |

## Offen

1. **§3 und `ci.yml`:** Der Vorschlag in §3 braucht einen neuen Schritt im CI-Job `plugin`. Owner-Entscheid 4 lautet „macOS-Nachtlauf und ci.yml unverändert“. Ob das auch diesen Schritt meint, ist gefragt (Chat, 26.11.2026). Entschieden: Owner-Antwort 1 (oben), der Schritt ist erlaubt.
2. **R3 außerhalb von `gate merge`:** Per Code gilt `evidence.required` auch für `quest verify`, am PR-Head, für `gate check` und in der Forge-Sicht von `quest list` und `status` (§1 „Folgen“). Empfehlung: so lassen, denn so ist der Schlüssel seit ADR-0007 B2 definiert, und eine eigene Liste je Aufrufer wäre neue Logik. Gegenargument: `quest verify` prüft am PR-Head dann ebenfalls nur drei Checks. Entschieden: Owner-Antwort 2 (oben), so lassen; der Beleg zu main steht in §1 „Folgen“.
3. **Rotes `build-test` auf develop mit R3 (nicht gebaut):** Heute meldet `gate merge` dann „develop läuft noch“, und kein Fix-PR kommt durch, wie schon vor R3 (§1). Möglich wäre eine Regel „ein roter Pflicht-Check geht vor einem fehlenden“: Dann hieße die Meldung „not green“, `gate check` meldete „red“, und ein belegter Fix-PR (ADR-0007 Q-0137) dürfte mergen. **Das ist eine Lockerung**, denn dieser Fix-PR-Weg ist heute zu. Empfehlung: nicht in diesem Gate, sondern als eigenes Issue mit eigenem Entscheid, denn es lockert eine Merge-Schutzregel. Gegenargument: Bis dahin bleibt bei rotem develop nur der Merge durch den Owner von Hand. Entschieden: Owner-Antwort 3 (oben), nicht bauen, eigenes Issue am Ende des Gates.
