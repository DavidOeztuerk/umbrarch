# ADR-0020: Selbst aufräumen (v0.2 Teil 1)

- Status: Eine frühere Fassung hat der Warden für develop freigegeben (schmale Prüfung des Fixes, approve, PR #163, Kommentar `#issuecomment-7079968810`). Die jetzige Fassung mit dem Bedrohungsmodell vom 25.11.2026 (§1) ist weder vom Warden noch vom Owner freigegeben. Owner-Fragen siehe „Offen“.
- Datum: 2026-11-22, §1 neu am 2026-11-25
- Gate: G-0130, Quests Q-0192 bis Q-0210
- Bezug: #46, #161, #74, #139, #16, #81, #44, #36, ADR-0003, ADR-0004, ADR-0007, ADR-0008, ADR-0010, ADR-0011, ADR-0012, ADR-0015, ADR-0018, ADR-0019
- Fassung: Designrunde 1 (`#issuecomment-7078646801`), Delta-Runde (`#issuecomment-7079794677`), Fix und schmale Prüfung (`#issuecomment-7079968810`); §1 danach nach der Schlussrunde (`#issuecomment-7161665432`) und der Delta-Runde zu Q-0200 (`#issuecomment-7167800109`)

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

1. **Haupt-Checkout:** Drei Schichten sollen den Hauptordner eines Projekts mit Monarch schützen. Die ersten beiden hat der Owner am 26.11.2026 aktiviert.
   - Die Sandbox von Claude Code steht in den User-Settings des Owners und gilt für jede Session. Bash schreibt nur im Arbeitsverzeichnis, im Temp-Ordner, in wenigen freigegebenen Ordnern und, aus einer Worktree, in Teilen von `.git`. HEAD, Index und die losen Refs von develop und main des Hauptordners sind gesperrt. `packed-refs` und `commondir` kommen mit dem Befehl aus der „Dritten Runde“ dazu, sobald der Owner ihn ausführt.
   - Ein git-Hook verweigert in Claude-Code-Sessions jede Änderung an develop und main, im Hauptordner auch an HEAD und dessen Branch. Neue Gate-Branches bleiben möglich.
   - Der Monarch-Hook verweigert bei Bash weiter, ist dort aber nur Frühwarnung und keine Sicherheitsgrenze. Edit und Write in den Hauptordner verweigert er. Ist eine Session im Hauptordner gestartet, verweigert er jeden Bash-Aufruf und jedes Schreibwerkzeug, auch nach `cd` woandershin; Lesen bleibt erlaubt.
   - Dazu kommen Permission-Regeln in den User-Settings, die Edit und Write auf die Einträge des Hauptordners sperren, unabhängig vom Monarch-Hook (Befehl aus der „Dritten Runde“).

   Gearbeitet wird nur in der Gate-Worktree.
2. **Mana bei Shadows:** Vor dem Start eines Shadows prüft der Hook, ob dessen typische Kosten noch unter die Grenze passen. Die Kosten sind aus den Ledgern gemessen. Erreicht ein laufender Shadow die Grenze, bekommt er keine Werkzeuge mehr und meldet seinen Stand als unvollständig. Ohne frischen Stand (headless) bremst nur der letzte bekannte Wert.
3. **Nach dem Reset:** Zeigt der Stand ein Fenster, das schon zurückgesetzt ist, sagt Monarch „Reset erfolgt, Stand wird erneuert“. Die Session prüft dann jede Minute neu, höchstens 10 Minuten lang (Vorschlag). Der Weckruf kommt eine Minute nach dem Reset (selten zwei, wegen der Jitter-Minuten) statt nach drei.
4. **Status auf develop:** develop nimmt keinen Commit ohne grüne Pflicht-Checks an. Der Stand nach dem Merge kommt darum erst mit dem nächsten Gate nach develop: Ein neuer Befehl holt ihn in die neue Worktree (Vorschlag, Offen 2).
5. **Quest verschieben:** Ein Befehl hängt eine Quest in ein anderes Gate um, in beiden Gate-Dateien und in der Quest-Datei, und schreibt das ins Ledger.
6. **Aufräumen:** Ein Befehl erledigt das Aufräumen nach dem Merge: committen, pushen, Worktree entfernen, ohne `--force`.

## 1. Haupt-Checkout schützen

### Bedrohungsmodell

Nach der Delta-Runde zu Q-0200 (`#issuecomment-7167800109`, vier major) gilt der Owner-Entscheid vom 25.11.2026, wörtlich:

> Root Cause: Shell-Parsing ist die falsche Ebene für protect_main_checkout. zsh-Semantik ist durch statische Analyse nicht vollständig vorhersagbar, deshalb findet jede Runde neue Schreibweisen. Ich will keine weitere Runde über cd/pushd-Schreibweisen und keine Positivliste.
>
> Neues Bedrohungsmodell für ADR-0020 §1: Der Haupt-Checkout wird auf OS- und git-Ebene geschützt. Der PreToolUse-Hook für Bash ist nur noch Frühwarnung und keine Sicherheitsgrenze.

Q-0201 und Q-0202 setzen die Schichten 2 und 3 im Repo um. Schicht 1 fügt der Owner in seine User-Settings ein, den git-Hook installiert er selbst (Befehle unten). Am 26.11.2026 hat der Owner beides aktiviert; die Ergebnisse stehen unter „Test nach der Aktivierung“. Die drei Schichten (Zusammenfassung):

1. **Schicht 1, OS-Sandbox:** Claude Code führt die Bash-Befehle jeder Session und ihrer Subagenten in einer Sandbox aus. Schreiben dürfen sie im Arbeitsverzeichnis, im Temp-Ordner, in den Pfaden aus `allowWrite` und, wenn das Arbeitsverzeichnis eine Worktree ist, im gemeinsamen `.git` des Haupt-Checkouts; dort liegen Objekte und Refs der Worktree. Im `.git` des Haupt-Checkouts sperrt `denyWrite` für jede Session `HEAD`, `index`, `ORIG_HEAD`, `FETCH_HEAD` und die losen Refs von develop und main. Mit dem Befehl aus der „Dritten Runde“ kommen `packed-refs` und `commondir` dazu (Owner-Entscheide 1 und 2 vom 26.11.2026); bis zur Ausführung ist `packed-refs` aus einer Worktree schreibbar (Warden-Befund 2, `#issuecomment-7185290617`).
2. **Schicht 2, git-Hook `reference-transaction`:** git ruft ihn vor jeder Ref-Änderung auf. In einer Claude-Code-Session (`CLAUDECODE` nicht leer) verweigert er jede Änderung an `refs/heads/develop` und `refs/heads/main`, im Haupt-Checkout zusätzlich an `HEAD` und an der Branch, auf die dessen HEAD zeigt. Andere Refs sind erlaubt, auch neue Gate-Branches aus dem Haupt-Checkout.
3. **Schicht 3, PreToolUse-Hook:** Bei Bash Frühwarnung mit einem lesbaren Grund, keine Sicherheitsgrenze. Das cwd-Modell für `cd`, `pushd` und `popd` ist eingefroren. Edit, Write, MultiEdit und NotebookEdit laufen nicht in der Sandbox. Gegen sie stehen diese Regel und, nach dem Befehl aus der „Dritten Runde“, Edit-Deny-Regeln in den User-Settings für die 24 Einträge der obersten Ebene (nicht für `.git` und `.claude`). Ist die Startwurzel der Session ein Haupt-Checkout, verweigert die Regel jeden Bash-Aufruf und jedes Datei-Schreibwerkzeug (Option B, Q-0209).

**Restrisiko**, Owner-Entscheid vom 25.11.2026, wörtlich: „Vorsätzliches Umgehen per Skript, das CLAUDECODE entfernt und core.hooksPath umbiegt, kann ein gepacktes develop/main lokal löschen; das Remote schützt die GitHub-Branch-Protection.“

Owner-Entscheid vom 26.11.2026 dazu, wörtlich: „packed-refs (major 2): <main>/.git/packed-refs in denyWrite. Restrisiko im ADR und in plugin/README.md neu formulieren, nicht mehr auf packed-refs ausdehnen.“

Neue Fassung (Vorschlag, Zusammenfassung der Session; gilt, sobald der Befehl aus der „Dritten Runde“ ausgeführt ist):
- Das Restrisiko vom 25.11.2026 betrifft nur das Skript, das `CLAUDECODE` entfernt und `core.hooksPath` umbiegt. Es deckt nicht das direkte Schreiben von `packed-refs` ohne git. Das sperrt ab dann Schicht 1, ebenso `commondir`.
- Datei-Werkzeuge (Edit, Write, MultiEdit, NotebookEdit) laufen außerhalb der Sandbox. In `<main>/.git` und `<main>/.claude` sperrt sie keine Permission-Regel (Owner-Entscheid 1), nur Schicht 3. Ohne Plugin oder ohne Hook ist dort keine Sperre.
- Ohne Plugin oder ohne Hook gibt es auch keine Sperre nach der Startwurzel (Option B). Startet eine Session dann im Haupt-Checkout, darf Bash dort neue Einträge der obersten Ebene anlegen (Warden-Befund 3); Option C und die Edit-Regeln decken nur die heutige Liste.
- Das Remote schützt die Branch-Protection auf GitHub (Beleg unten).

Beleg zur Branch-Protection, Stand 25.11.2026 (`gh api repos/DavidOeztuerk/umbrarch/branches/develop/protection`, dasselbe für `main`): Beide Branches haben `allow_deletions: false`, `allow_force_pushes: false`, `enforce_admins: true` und Pflicht-Checks. main hat zusätzlich `lock_branch: true`.

### Schicht 1: Sandbox

Owner-Vorgaben vom 25.11.2026, wörtlich. Mit dem Bedrohungsmodell: „sandbox.enabled true, failIfUnavailable true, allowUnsandboxedCommands false.“ Nach dem Bericht zu Q-0201: „Schicht 1 kommt in meine User-Settings ~/.claude/settings.json, nicht ins Repo. Absolute Pfade, keine "..". Dann gilt die Sandbox für jede Session, auch im Haupt-Checkout, in älteren Worktrees und in anderen Projekten. Das ersetzt meine Vorgabe "in .claude/settings.json im Repo".“ und „denyWrite absolut für /Users/davidozturk/Projects/umbrarch/.git/{HEAD,index,ORIG_HEAD,FETCH_HEAD,refs/heads/develop,refs/heads/main}.“ Dazu: „Keine excludedCommands vorsorglich. Erst alles ohne Ausnahme planen; nach meiner Aktivierung testen; eine Ausnahme nur, wenn ein konkreter Fehler belegt ist und allowWrite/allowedDomains ihn nicht lösen. "gh api *" nie pauschal, nur konkrete Unterbefehle.“

Was die Claude-Code-Doku sagt (`code.claude.com/docs/en/sandboxing` und `code.claude.com/docs/en/settings-reference`, gelesen am 25.11.2026; Zitate wörtlich, Abschnitt in Klammern):
- **Schreibbereich:** „By default, sandboxed commands can write to the current working directory, the per-user temp directory“ (Configure sandboxing). In der Tabelle unter „What the sandbox restricts“ steht für Writes: „The working directory, a per-user temp directory, and directories you've added.“ `allowWrite` fügt Pfade hinzu: „Add paths where sandboxed commands can write, beyond the working directory, the per-user temp directory, and the directories you've added“. Ausdrücklich verboten nennt die Doku den Rest nicht; sie zählt nur die erlaubten Orte auf. Für eine Session in einer Gate-Worktree liegt der Arbeitsbaum des Haupt-Checkouts außerhalb dieser Orte. Bestätigt hat das der Beweis `touch <main>/SANDBOX-PROBE` nach der Aktivierung („Test nach der Aktivierung“).
- **Worktrees:** „when the working directory is a linked git worktree, the sandbox also allows writes to the main repository's shared `.git` directory“ und „Writes to `hooks/` and `config` inside that directory remain denied.“ (How sandboxing works > Filesystem isolation). Laut settings-reference gilt das auch für eine Worktree, „including one Claude Code entered mid-session“.
- **Pfade in User-Settings:** `/` heißt „Absolute path from filesystem root“, `~/` heißt „Relative to home directory“ (Sandbox path prefixes).
- **User- und Projekt-Settings:** „Claude Code merges them, combining the paths rather than replacing one scope's array with another's.“ (Configure sandboxing).
- **Netzwerk:** „your allowed domains start empty“ (Network isolation). Ob `*.example.com` auch `example.com` abdeckt, sagt die Doku nicht.
- **TLS auf macOS:** „Tools such as `gh`, `gcloud`, and `terraform` may fail TLS verification under Seatbelt.“ (Troubleshooting > Go-based CLIs fail TLS verification on macOS). Die Doku schlägt dort `gh *` in `excludedCommands` vor. Nach dem Owner-Entscheid geht das erst nach einem belegten Fehler und nur mit engen Mustern. Die Alternative `enableWeakerNetworkIsolation` heißt „Let sandboxed commands on macOS reach the system TLS trust service, `com.apple.trustd.agent`.“ und trägt die Warnung „This reduces security by opening a potential data exfiltration path through the trust service.“
- **Nicht in der Doku:** ob monarch (.NET), `dotnet restore` und `git push` unter Seatbelt die TLS-Prüfung und den Schlüsselbund erreichen. Am 25.11.2026 lagen die Anmeldungen im Schlüsselbund: gh mit „(keyring)“ laut `gh auth status`, git mit `credential.helper=osxkeychain` aus der Xcode-gitconfig. Das zeigt erst der Test nach der Aktivierung.
- Zusammenfassung aus derselben Doku: Subagenten laufen im selben Prozess und mit derselben Sandbox wie die Session. Hooks laufen außerhalb der Sandbox. Die Datei-Werkzeuge (Edit, Write) folgen den Permission-Regeln, nicht der Sandbox. `.claude`-Settings im Arbeitsverzeichnis und darüber kann Bash nicht ändern.

**Inhalt (Vorschlag) für `~/.claude/settings.json`, Schlüssel `sandbox`:**

```json
{
  "enabled": true,
  "failIfUnavailable": true,
  "allowUnsandboxedCommands": false,
  "filesystem": {
    "denyWrite": [
      "/Users/davidozturk/Projects/umbrarch/.git/HEAD",
      "/Users/davidozturk/Projects/umbrarch/.git/index",
      "/Users/davidozturk/Projects/umbrarch/.git/ORIG_HEAD",
      "/Users/davidozturk/Projects/umbrarch/.git/FETCH_HEAD",
      "/Users/davidozturk/Projects/umbrarch/.git/refs/heads/develop",
      "/Users/davidozturk/Projects/umbrarch/.git/refs/heads/main"
    ],
    "allowWrite": [
      "/Users/davidozturk/.nuget/packages",
      "/Users/davidozturk/.local/share/NuGet/http-cache",
      "/Users/davidozturk/.dotnet/TelemetryStorageService",
      "/private/tmp"
    ]
  },
  "network": {
    "allowedDomains": ["github.com", "api.github.com", "*.githubusercontent.com", "api.nuget.org"]
  }
}
```

**Begründung je Eintrag** (Erhebung der Session am 25.11.2026):
- `denyWrite`: Owner-Vorgabe.
- `~/.nuget/packages` und `~/.local/share/NuGet/http-cache`: die Standardorte von NuGet für Pakete und HTTP-Cache; beide Ordner gibt es. Bei einem Restore ohne neue Pakete blieben sie unverändert (`find … -mmin -14` nach `dotnet build --no-incremental`). Ohne die Einträge scheitert ein Restore, der neue Pakete lädt. `~/.nuget/NuGet` mit der Konfiguration bleibt gesperrt.
- `~/.dotnet/TelemetryStorageService`: Dort schrieb dotnet beim Build eine Datei (`20261125210630_….trn`, derselbe `find`).
- `/private/tmp` in `allowWrite`: `lsof -U` während der Läufe zeigte Unix-Sockets unter `/tmp`, außerhalb von `$TMPDIR` (`/var/folders/…`). Bei `dotnet build` war es `/tmp/MSBuild…`, bei `dotnet test` `/tmp/<32 Hex>`. Beim Build entstand dazu ein Socket `/private/tmp/<43 Zeichen>`.
- Kein `allowUnixSockets`. Owner-Entscheid vom 26.11.2026, wörtlich: „Settings wie vorgeschlagen, aber OHNE "allowUnixSockets": ["/private/tmp"]. Grund: In /private/tmp liegen auf macOS der ssh-agent-Socket (com.apple.launchd.*/Listeners, SSH_AUTH_SOCK) und tmux-Sockets; die Freigabe wäre ein Weg aus der Sandbox.“ Was dotnet ohne die Freigabe tut, steht unter „Test nach der Aktivierung“.
- `github.com`: git push und fetch über https (Remote `https://github.com/DavidOeztuerk/umbrarch.git`). `api.github.com`: gh und monarch (Host in `src/Monarch.Cli/Support.cs:176` und `src/Monarch.Infrastructure.Evidence/EvidenceBuilderExtensions.cs:20`). Beide stehen einzeln da, weil die Doku zur Basisdomain schweigt. `*.githubusercontent.com`: Owner-Vorgabe („GitHub inkl. *.githubusercontent.com“); welche Hosts gh dort braucht, zeigt der Test.
- Alle Pfade sind absolut, wie es der Owner vorgibt („Absolute Pfade, keine ".."“).
- `api.nuget.org`: einzige Quelle in `nuget.config:4-5` (`<clear />`, dann nuget.org). Der Service-Index (`https://api.nuget.org/v3/index.json`) nennt außerdem `azuresearch-usnc.nuget.org`, `azuresearch-ussc.nuget.org` und `www.nuget.org` für Suche und Webseiten. Einen eigenen CDN-Host nennt er nicht.
- `~/.monarch`: kein Eintrag. monarch-Befehle, die per Bash laufen, schreiben ins Projekt (`.monarch/` der Worktree). Nach `~/.monarch` schreiben nur Statusline (`Statusline.cs:123`, `mana.json`) und Hook (`BackgroundShells.cs:24`, `:72`). Beide laufen außerhalb der Sandbox, denn sie gilt laut Doku nur für Bash-, PowerShell- und Monitor-Befehle und deren Kindprozesse (Zusammenfassung). Ausnahme ist `monarch wake schedule`: nur mit Opt-in, es schreibt nach `~/Library/LaunchAgents` (`WakeCommands.cs:161-162`). So schützt die Sandbox `~/.monarch/**`. protect_paths im Hook-Text (G2 in `plugin/README.md`) ist dafür nicht die einzige Schranke.
- `excludedCommands`: keine (Owner-Entscheid).

Folgen (Zusammenfassung): Die Sandbox gilt für jede Session in jedem Projekt. Andere Projekte brauchen vielleicht weitere Domains; im Auto-Modus lehnt Claude Code fremde Hosts ab (Abschnitt „Hosts outside your allowed domains“). Mit `failIfUnavailable` startet Claude Code nicht, wenn die Sandbox nicht startet; laut settings-reference „Claude Code exits with an error at startup when `sandbox.enabled` is `true` but the sandbox can't start.“

**Befehl für den Owner (Schicht 1).** Er legt zuerst eine Sicherung `~/.claude/settings.json.bak-<datum-zeit>` an und fügt dann mit python3 den Schlüssel `sandbox` ein. Er bricht ab, ohne etwas zu ändern, wenn die Datei fehlt, kein gültiges JSON ist oder `sandbox` schon existiert. Die Werte der anderen Schlüssel prüft er nach dem Schreiben; die Einrückung der Datei wird neu gesetzt. Gelöscht wird nichts. Ist die Datei ein Symlink, ändert er das Ziel des Links, der Link bleibt. Getestet am 25.11.2026 im Scratchpad unter zsh (nur der Pfad ersetzt), damals noch mit der Zeile `allowUnixSockets`: Einfügen, zweiter Lauf mit Abbruch, fehlende Datei mit Abbruch, Symlink (Link bleibt, Ziel bekommt `sandbox`). Ohne diese Zeile hat der Owner den Befehl am 26.11.2026 ausgeführt. Der Block in `~/.claude/settings.json` hat danach dieselben Werte wie der Inhalt oben (gelesen mit dem Read-Werkzeug am 26.11.2026, Zeilen 99-127). Anders als bei diesem Befehl steht dort `network` vor `filesystem`; die Reihenfolge der Schlüssel ändert in JSON nichts. Spätere Ergänzungen: „Zweite Runde nach der Aktivierung“.

```sh
python3 -I - "$HOME/.claude/settings.json" <<'PY'
import json, os, shutil, sys, time
path = os.path.realpath(sys.argv[1])
main = "/Users/davidozturk/Projects/umbrarch/.git/"
block = {
    "enabled": True,
    "failIfUnavailable": True,
    "allowUnsandboxedCommands": False,
    "filesystem": {
        "denyWrite": [main + p for p in ["HEAD", "index", "ORIG_HEAD", "FETCH_HEAD", "refs/heads/develop", "refs/heads/main"]],
        "allowWrite": ["/Users/davidozturk/.nuget/packages", "/Users/davidozturk/.local/share/NuGet/http-cache", "/Users/davidozturk/.dotnet/TelemetryStorageService", "/private/tmp"],
    },
    "network": {
        "allowedDomains": ["github.com", "api.github.com", "*.githubusercontent.com", "api.nuget.org"],
    },
}
if not os.path.isfile(path):
    sys.exit(f"Abbruch: {path} fehlt, nichts geändert")
with open(path, encoding="utf-8") as fh:
    raw = fh.read()
try:
    data = json.loads(raw)
except json.JSONDecodeError as e:
    sys.exit(f"Abbruch: {path} ist kein gültiges JSON ({e}), nichts geändert")
if not isinstance(data, dict):
    sys.exit("Abbruch: oberste Ebene ist kein Objekt, nichts geändert")
if "sandbox" in data:
    sys.exit('Abbruch: "sandbox" existiert schon, nichts geändert')
backup = f"{path}.bak-{time.strftime('%Y%m%d-%H%M%S')}"
tmp = f"{path}.tmp-sandbox"
for p in (backup, tmp):
    if os.path.exists(p):
        sys.exit(f"Abbruch: {p} existiert schon, nichts geändert")
shutil.copy2(path, backup)
new = dict(data)
new["sandbox"] = block
with open(tmp, "w", encoding="utf-8") as fh:
    json.dump(new, fh, indent=2, ensure_ascii=False)
    fh.write("\n")
with open(tmp, encoding="utf-8") as fh:
    check = json.load(fh)
if {k: v for k, v in check.items() if k != "sandbox"} != data or check.get("sandbox") != block:
    sys.exit(f"Abbruch: Prüfung fehlgeschlagen, {path} unverändert, Entwurf liegt in {tmp}")
os.chmod(tmp, os.stat(path).st_mode & 0o777)
os.replace(tmp, path)
print(f"Sicherung: {backup}")
print(f'"sandbox" eingefügt in {path}; alle anderen Schlüssel unverändert (Werte geprüft, Einrückung neu).')
PY
```

**Session im Haupt-Checkout, neu bewertet.** Dann ist der Haupt-Checkout das Arbeitsverzeichnis, und Bash darf dort Dateien im Arbeitsbaum schreiben. Die sechs absoluten `denyWrite`-Pfade gelten auch dort. HEAD und dessen Branch verweigert zusätzlich Schicht 2. Das ist die einzige Lage, in der Bash den Arbeitsbaum des Haupt-Checkouts schreiben kann. Der SessionStart-Hook warnt (Q-0201); den Vorschlag dazu enthält der Abschnitt „Session im Haupt-Checkout“ unten.

**Entfallen** mit den User-Settings: die Lücken „ältere Worktrees ohne Datei“ und „andere Projekte ohne Sandbox“. Eine Session in einem anderen Projekt kann weder den Arbeitsbaum noch das `.git` des Haupt-Checkouts schreiben, denn beide liegen außerhalb ihrer Schreiborte (Doku-Beleg oben; der Beweis nach der Aktivierung steht aus). Damit erledigt sich auch der Hinweis zu major 4 im Vermerk in #164 (`git checkout -- <pfad>` aus einem anderen Projekt).

### Test nach der Aktivierung (26.11.2026)

Session in der Gate-Worktree G-0130, Sandbox aus den User-Settings ohne `allowUnixSockets`, Hook installiert (`cmp` mit `plugin/git-hooks/reference-transaction`: identisch; `git config --get core.hooksPath`: Exit 1). Fehlertexte wörtlich.

| Befehl | Ergebnis |
|---|---|
| `touch <main>/SANDBOX-PROBE` | „Operation not permitted“, Exit 1, keine Datei |
| `: >> <main>/.git/index` | „operation not permitted“, Exit 1, Größe und Zeit des Index unverändert |
| `dotnet build -warnaserror` | hängt; nach 10 min „Fehler beim Buildvorgang. 0 Warnung(en) 0 Fehler“, Exit 1 |
| `dotnet build -warnaserror -m:1` oder `--disable-build-servers` allein | hängt nach über 3 min, abgebrochen |
| `dotnet build -warnaserror --disable-build-servers -m:1 -nodeReuse:false` | grün, 0 Warnungen, 26,8 s |
| `dotnet test --no-build --project tests/Monarch.Domain.Tests` | Exit 134, „System.Net.Sockets.SocketException (13): Permission denied“ in `TestApplication.WaitConnectionAsync` |
| Test-Apps direkt (`tests/Monarch.<X>.Tests/bin/Debug/net10.0/Monarch.<X>.Tests`) | Domain 97, Policy 1443, Infrastructure 191 (1 übersprungen) ohne Fehler; Cli 755 mit 1 Fehler: `Real_ps_finds_the_hull_of_a_real_child_and_the_hook_leaves_it_running`, „An error occurred trying to start process '/bin/ps' … Operation not permitted“ |
| `dotnet restore` ohne Änderung | „Alle Projekte sind für die Wiederherstellung auf dem neuesten Stand.“, Exit 0 |
| `dotnet restore --force-evaluate --no-http-cache` | NU1900 „Der Dienstindex für die Quelle "https://api.nuget.org/v3/index.json" konnte nicht geladen werden.“, Exit 1 |
| Kleines .NET-Programm, `HttpClient` mit `HttpClientHandler` wie `Support.cs` | api.github.com und api.nuget.org: „HttpRequestException: The SSL connection could not be established“, innen „AppleCommonCryptoCryptographicException“ |
| `monarch quest list` | „forge not reachable for #29 (HttpRequestException), … #163 (HttpRequestException)“ |
| `gh pr checks 163`, `gh auth status` | „tls: failed to verify certificate: x509: OSStatus -26276“ |
| `curl https://api.github.com/zen`, `curl https://api.nuget.org/v3/index.json` | je HTTP 200 |
| `git fetch origin` | Exit 0 |
| `git push origin HEAD:refs/heads/monarch/G-0130-selbst-aufraeumen` (Ledger-Commit 9c2fea6) | „0675857..9c2fea6“, Exit 0; Anmeldung über den Schlüsselbund geht |
| `monarch mana` | Exit 0 (lokal) |
| Knight und Warden, je ohne und mit `isolation: worktree`: `touch <main>/SANDBOX-PROBE-<agent>` | je „Operation not permitted“, Exit 1, kein Hook-Deny |
| dieselben: `touch ./.sandbox-probe-<agent>` im eigenen Arbeitsverzeichnis | je Exit 0; mit Isolation lag es unter `.claude/worktrees/agent-…` |
| `mkdir .claude/skills` in der Gate-Worktree (`denyWrite` der Sandbox innerhalb des Arbeitsverzeichnisses, Pfad fehlt) | „Operation not permitted“ |
| `mv .claude/probe-dir .claude/skills` | „rename … Operation not permitted“ |

Lesart der Session:
- Ohne Socket-Freigabe scheitern die Verbindungen von MSBuild zu Knoten und Build-Servern sowie von `dotnet test` zur Test-App. Die drei Schalter beim Build und der direkte Start der Test-Apps kommen ohne Freigabe aus. Den Socket-Pfad von `dotnet test` zeigt die Sandbox nicht, weil schon das Anlegen scheitert. Darum schlägt die Session keinen engen Socket-Pfad vor.
- gh (Go) und .NET prüfen TLS über den Trust-Dienst von macOS und scheitern. curl und git kommen durch. Lösungsvorschlag unter „Offen“.
- `/bin/ps` darf in der Sandbox nicht starten. Der Hook läuft außerhalb der Sandbox (Doku-Zusammenfassung oben); der eine rote Test ist eine Grenze der Sandbox und kein Fehler im Code.
- Mit `isolation: worktree` legt Claude Code die Worktree auf `origin/HEAD` an (8caa41c, origin/main), nicht auf develop. Für die Dauer des Agenten stand die Agent-Worktree auch im Schreibbereich der Session. Nach dem Ende blieben beide Worktrees stehen.
- `denyWrite` gilt auch für Pfade, die es noch nicht gibt, und für das Ziel einer Umbenennung (Grundlage für Option C unten). Nicht getestet ist das Umbenennen eines Ordners über einem gesperrten Pfad (etwa `.git/refs` mit `refs/heads/develop` darin). Das prüft der Warden gezielt.

### Zweite Runde nach der Aktivierung (26.11.2026)

Owner, wörtlich: „Option C ist ausgeführt (24 Einträge in denyWrite).“ und „Ich habe sandbox.enableWeakerNetworkIsolation: true gesetzt (dein Befehl 2).“ Damit weichen die User-Settings vom Inhalt (Vorschlag) oben in zwei Punkten ab: `enableWeakerNetworkIsolation` und die 24 Einträge aus Option C in `denyWrite`.

Owner-Entscheide vom 26.11.2026, wörtlich:
- „TLS: enableWeakerNetworkIsolation wie empfohlen, keine excludedCommands.“
- „dotnet lokal: neue Quest in G-0130. scripts/test-local.sh baut mit --disable-build-servers -m:1 -nodeReuse:false und startet die vier Test-Apps direkt, Exitcode = Fehler, wenn eine App fehlschlägt. Den /bin/ps-Test nicht aufweichen und nicht ausfiltern; das Skript nennt ihn als bekannte Grenze der Sandbox. Standardregel und Knight-Skill: lokale Tests über dieses Skript. CI unverändert.“
- „Neuer Befund isolation: worktree (Basis origin/HEAD = main): Regel in die Skills, dass Knights nie mit isolation: worktree laufen; Punkt in #164.“
- „/bin/ps-Test: bleibt als bekannte Grenze. Aufräumen nach dem Merge: gate cleanup mache ich vorerst im eigenen Terminal; SKILL.md:16-17 und :72 entsprechend anpassen; die Frage "worktree remove aus der Sandbox" kommt ins Issue zu gate open (anlegen, Milestone v0.2).“

Umgesetzt in Q-0205: `scripts/test-local.sh`, Standardregel 2 (Aufräumen, Checkliste Schritt 10, Fallen-Liste), `plugin/skills/quest/SKILL.md`, `plugin/agents/knight.md`, Tests in `RegelnTextTests`.

**Nachtest TLS** (mit `enableWeakerNetworkIsolation`):

| Befehl | Ergebnis |
|---|---|
| `gh pr checks 163` | Exit 0, vier Checks `pass` |
| `monarch quest list` | Exit 0, ohne „forge not reachable“, Status „cleared (Forge)“ aus GitHub |
| `dotnet restore --force-evaluate --no-http-cache` (normaler Audit) | Exit 0 |
| danach `dotnet restore` | „Alle Projekte sind für die Wiederherstellung auf dem neuesten Stand.“, Exit 0 |
| `scripts/test-local.sh` | Exit 1: Build grün; Domain 97/0, Policy 1443/0; Infrastructure 1 Fehler `LedgerHeadTreeTests.A_missing_blob_in_an_existing_repository_is_a_finding_not_unchanged` (einzeln dreimal grün, im vollen Lauf Befund für `.monarch/ledger` statt für die Datei; zeitabhängig, vorbestehend, Punkt in #164; Owner-Entscheid vom 26.11.2026, wörtlich: „Schwankender Ledger-Test bleibt in #164, kein Fix in G-0130.“); Cli 758 mit 1 Fehler, dem bekannten `/bin/ps`-Test |

**Proben für den Beweis** (Definition des Owners, laut Owner aus seinem Prompt vom 25.11.2026, im Chat vom 26.11.2026 wörtlich wiederholt; a4 danach auf Owner-Entscheid korrigiert, siehe unter der Tabelle; eine zsh-Datei im Scratchpad, `zsh <datei>`; nach jeder Probe Vergleich von develop, HEAD, `ls -l .git/index`, getrackten Änderungen, `index.lock` und `SANDBOX-PROBE`):

| Probe | pwd vor `git commit` | Ausgabe | Exit | Zustand |
|---|---|---|---|---|
| a1 `cd <main>; cd /tmp; cd -d /tmp; cd -` | `<main>` | `cd: string not in pwd: -d`, dann „fatal: unable to write new index file“ | 128 | unverändert |
| a2 `mkdir -- -x && cd -x/../../../..` | `<main>` | „fatal: unable to write new index file“ | 128 | unverändert |
| a3 `cd <main>; pushd /tmp; cd -- +1` | `<main>` | „fatal: unable to write new index file“ | 128 | unverändert |
| a4 `cd G-0130 ../..` | `<main>` | „fatal: unable to write new index file“ | 128 | unverändert |
| b `git -C <main> commit --allow-empty -m SANDBOX-PROBE` | – | „fatal: unable to write new index file“ | 128 | unverändert |
| c `touch <main>/SANDBOX-PROBE` | – | „Operation not permitted“ | 1 | unverändert |
| d `git -C <main> reset --hard` | – | „fatal: Could not write new index file.“ | 128 | unverändert |

Vorher und nachher: develop `dfa56649c346e34229e3d093c524a1c43c089ffa`, keine geänderten getrackten Dateien, `.git/index` 52290 Bytes vom 22. Nov., kein `index.lock`. Letzter Commit: `dfa56649…` „G-0129: Mana-Grenze = Arbeitsgrenze, je Plan und Modell (#156) (#160)“. Den Ordner `-x` hat das Skript danach entfernt.

Korrektur a4, Owner-Entscheid vom 26.11.2026, wörtlich: „Probe a4: in der Definition auf ../.. korrigieren (a4b wird a4), im ADR vermerken.“ In der ersten Fassung stand a4 als `cd G-0130 ../../..`. Damit landete pwd in `/Users/davidozturk/Projects` statt in `<main>`, und git meldete „fatal: not a git repository (or any of the parent directories): .git“ (Exit 128, Zustand unverändert). Die Zusatzprobe a4b der Session mit `../..` ist jetzt a4.

Lesart der Session: Alle Proben scheitern an Schicht 1. a1 bis a4, b und d scheitern beim Schreiben des Index, c beim Anlegen der Datei. Schicht 2 kommt dabei nicht zum Zug.

**Probe e, Schicht 2 allein.** Definition des Owners vom 26.11.2026, wörtlich: „e) aus der Gate-Worktree: git update-ref -d refs/heads/develop dfa56649c346e34229e3d093c524a1c43c089ffa develop liegt nur in packed-refs; packed-refs ist aus der Worktree schreibbar. Erwartet: Abbruch durch den Hook mit Verweis auf ADR-0020 §1, develop danach unverändert (rev-parse vorher/nachher, grep in packed-refs).“ Rahmen: Session in der Gate-Worktree G-0130, `CLAUDECODE` gesetzt, Hook identisch mit `plugin/git-hooks/reference-transaction` laut `cmp`, `core.hooksPath` nicht gesetzt, `.git/refs/heads/develop` fehlt (`ls -l`), develop steht in `packed-refs` Zeile 2.

Dass Schicht 1 hier nicht greift, ist gefolgert und nicht getestet (Lesart der Session): `packed-refs` steht nicht in `denyWrite` (Inhalt oben und die 24 Einträge aus Option C, die `.git` auslassen), und laut Doku ist das gemeinsame `.git` aus einer Worktree schreibbar. Eine Probe, die `packed-refs` ohne Schicht 2 schreibt, gibt es nicht; sie würde develop löschen.

| Befehl | Ausgabe | Exit | Zustand |
|---|---|---|---|
| `git update-ref -d refs/heads/develop dfa56649c346e34229e3d093c524a1c43c089ffa` | „Ref-Aenderung verweigert (ADR-0020 §1): refs/heads/develop\|main, im Haupt-Checkout auch HEAD und dessen Branch. Arbeite in der Gate-Worktree.“, dann „fatal: in 'prepared' phase, update aborted by the reference-transaction hook“ | 128 | unverändert |

Vorher und nachher: `git rev-parse refs/heads/develop` gibt `dfa56649c346e34229e3d093c524a1c43c089ffa`; `grep -n 'refs/heads/develop$' .git/packed-refs` gibt `2:dfa56649c346e34229e3d093c524a1c43c089ffa refs/heads/develop`; `packed-refs` 5720 Bytes vom 22. Nov.; kein `packed-refs.lock`, kein `refs/heads/develop.lock`. Schicht 2 hat verweigert, wie der Owner es erwartet hat.

### Dritte Runde nach dem Verdict changes (26.11.2026)

Warden-Runde über Q-0201 bis Q-0206 am Head bcf4dcb: changes, drei major (`#issuecomment-7185290617`). Owner-Entscheide vom 26.11.2026, wörtlich:

> 1. commondir (major 1): a+b+c wie empfohlen, als heikle Quest in G-0130, roter Test zuerst. Schicht 3 erkennt den Haupt-Checkout per realpath-Vergleich von git-dir und git-common-dir wie der git-Hook, nicht über das Fehlen von commondir (ProtectMainCheckoutRule.cs:112, HookRunner.cs:532). <main>/.git/commondir kommt in denyWrite. Zusätzlich: Permission-Regeln permissions.deny "Edit(//Users/davidozturk/Projects/umbrarch/<eintrag>/**)" bzw. für Dateien ohne /** für die 24 Einträge aus Option C, damit Edit/Write/MultiEdit/NotebookEdit unabhängig von Schicht 3 gesperrt sind. Belege aus der Doku, ob Edit-Regeln alle Datei-Werkzeuge abdecken und wie sie mit sandbox.denyWrite zusammengeführt werden; KEINE Edit-Regel auf <main>/.git oder <main>/.claude.
>
> 2. packed-refs (major 2): <main>/.git/packed-refs in denyWrite. Restrisiko im ADR und in plugin/README.md neu formulieren, nicht mehr auf packed-refs ausdehnen.
>
> 3. Session im Haupt-Checkout (major 3): Option B jetzt bauen, aber über CLAUDE_PROJECT_DIR statt SessionStart-Zustand: Ist die Session-Wurzel (realpath von CLAUDE_PROJECT_DIR) ein Haupt-Checkout, verweigert PreToolUse jeden Bash-Aufruf und jedes Datei-Schreibwerkzeug mit dem Grund "Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu". Lesen bleibt erlaubt. Belege aus der Hooks-Doku, dass CLAUDE_PROJECT_DIR die Startwurzel ist und sich bei EnterWorktree/cd nicht ändert. Roter Test zuerst. Option C bleibt zusätzlich; das Restrisiko "ohne Plugin/Hook" benennen.
>
> 4. Minors nach #164. Zusätzlich testen: Läuft scripts/test-local.sh ohne allowWrite /private/tmp grün (bis auf die bekannten zwei)? Teste das, indem du TMPDIR-unabhängig prüfst, ob Build und Test-Apps nach /private/tmp schreiben (ohne die Settings zu ändern, z. B. per Beobachtung der Dateien in /private/tmp während eines Laufs). Wenn nichts geschrieben wird: schlag mir vor, den Eintrag zu entfernen.

Umgesetzt:
- **Q-0208** (Entscheid 1 a und c): `IsMainCheckoutGitDir` in `src/Monarch.Policy/ProtectMainCheckoutRule.cs` vergleicht realpath von git-dir und git-common-dir. Das Briefing nutzt dieselbe Prüfung (`ToolCallPolicy.IsInMainCheckout`, `src/Monarch.Cli/HookRunner.cs`). Rote Tests vorher: 7 in `ProtectMainCheckoutTests` (`commondir` = `.`, `./`, `../.git`, `../.GIT`, absolut über Symlink, leer, Ziel fehlt), 2 in `MainCheckoutHookTests` (Briefing warnt, echter Hook verweigert Write). Ein Ordner namens `commondir` war schon vorher gesperrt.
- **Q-0209** (Entscheid 3): Option B, `ToolCallPolicy.MainCheckoutSession` und `PolicyContext.SessionRoot` (aus `CLAUDE_PROJECT_DIR`). Rote Tests vorher: 6 in `ProtectMainCheckoutTests` (Bash und die vier Schreibwerkzeuge, Wurzel über Symlink und Unterordner), 2 in `MainCheckoutHookTests` (echter Hook, auch mit cwd außerhalb jedes Projekts). Kein Deny dieser Art ohne `CLAUDE_PROJECT_DIR`, mit einer Worktree oder einem nicht erweckten Repo als Wurzel.
- `scripts/test-local.sh` danach: Build 0 Warnungen; Domain 97/0, Policy 1462/0, Infrastructure 191/0 (1 übersprungen), Cli 763 mit 1 Fehler, dem bekannten `/bin/ps`-Test.

**Doku-Belege** (gelesen am 26.11.2026, Markdown-Fassungen unter `code.claude.com/docs/en/…`; Zitate wörtlich, Abschnitt in Klammern):
- Edit-Regeln und Datei-Werkzeuge (permissions): „`Edit` rules apply to all built-in tools that edit files.“ (What `Read` and `Edit` rules cover) und „`Write`, `NotebookEdit`, or legacy `MultiEdit`: use `Edit(docs/**)` in place of `Write(docs/**)`.“ (`Write(path)`, `Glob(path)`, and `Grep(path)` rules don't match).
- Pfade (permissions, How path patterns match): „Read and Edit rules both use gitignore pattern syntax with four distinct pattern types:“; `//path` heißt „Absolute path from filesystem root“; „Use `//Users/alice/file` for absolute paths.“ Beispiel: „`Edit(//tmp/scratch.txt)`: edits the absolute path `/tmp/scratch.txt`“. Dass eine Regel ohne `/**` genau eine Datei trifft, sagt die Doku nicht ausdrücklich; das zeigt erst Probe h.
- Zusammenführung mit der Sandbox (sandboxing, How sandboxing relates to permissions and permission modes): „Paths and domains from both sandbox settings and permission rules are merged into the final sandbox configuration.“ Tabelle: „`Read` and `Edit` deny rules | Block access to specific files or directories“ und „`sandbox.filesystem.denyWrite` and `sandbox.filesystem.denyRead` | Block subprocess access to specific paths“. Außerdem: „The sandbox covers shell commands only. Claude's file tools, MCP servers, and hooks run outside it.“ Lesart der Session: Die Edit-Deny-Regeln sperren die Datei-Werkzeuge, und Claude Code nimmt ihre Pfade zusätzlich in die Sandbox auf. Ob sie dort genau wie `denyWrite` wirken, sagt die Doku nicht.
- Vorrang (permissions, Settings precedence): „If a tool is denied at any level, no other level can allow it.“ und (Manage permissions) „Rules are evaluated in order: deny, then ask, then allow.“ Zum Auto-Modus sagt die Doku an dieser Stelle nichts.
- Listen aus mehreren Settings (settings, Lists merge instead of overriding): „When you set the same list key, such as `permissions.allow`, in more than one file, Claude Code combines the lists instead of picking one“.
- `CLAUDE_PROJECT_DIR` (hooks, Reference scripts by path): „`${CLAUDE_PROJECT_DIR}`: the project root where the session started.“ Die Note „Worktrees are different.“: „Claude Code keeps `${CLAUDE_PROJECT_DIR}` where it was“ und „it still points at the project root where the session started“. Zu `cwd`: „the `cwd` field in the hook's input JSON is the worktree root after Claude enters a worktree“ und „and the new directory after Claude runs `cd`.“ Dazu (permissions, Move the session to another directory): „Hooks the move activates still receive `${CLAUDE_PROJECT_DIR}` set to the project root where the session started.“ EnterWorktree nennt die Doku dort nicht namentlich.
- Subagenten (hooks, Hook locations): „When a subagent calls a tool, tool events such as `PreToolUse` and `PostToolUse` fire the same configured hooks as in the main conversation“.

**Messung zu Entscheid 4** (`/private/tmp`, ohne die Settings zu ändern):
- Zwei Läufe von `scripts/test-local.sh`. Lauf 1: alle 0,5 s `find /private/tmp -maxdepth 3 -newer <marke>` ohne `/private/tmp/claude*`; Ausgabe nur `/private/tmp` selbst, mtime 09:55:27. Lauf 2: alle 0,05 s neue Namen der obersten Ebene (`ls -A /private/tmp | grep -v '^claude'` gegen den Stand vor dem Lauf); kein neuer Name, mtime danach 10:01:43. In fünf Minuten ohne Lauf (`stat` alle 0,2 s) blieb die mtime gleich.
- Ursache im Code: `tests/Monarch.Policy.Tests/PathCanonicalizationTests.cs:84` legt mit Absicht `/tmp/monarch-canon-<guid>` an (Test für den Alias `/tmp` ↔ `/private/tmp`, Umgehung 4) und löscht den Ordner gleich wieder. Ob es noch einen zweiten Schreiber gibt, zeigt die Messung nicht.
- Folge (Vorschlag): Ohne `allowWrite /private/tmp` würde dieser Test scheitern (Lesart der Session, nicht getestet, die Settings blieben unverändert). `scripts/test-local.sh` wäre also nicht mehr grün bis auf die bekannten Grenzen. Den Test aufzuweichen kommt nicht in Frage (Standardregel 4). Der Eintrag bleibt; der Befehl unten ändert `allowWrite` nicht. Die Folge der Freigabe nennt Warden-Befund 5 (Socket-Dateien in `/private/tmp` lassen sich löschen oder ersetzen, nicht anlegen); er steht in #164.

**Befehl für den Owner** (Entscheide 1 und 2, ein Lauf). Er legt zuerst eine Sicherung `~/.claude/settings.json.bak-<datum-zeit>` an. Dann ergänzt er `sandbox.filesystem.denyWrite` um `<main>/.git/commondir` und `<main>/.git/packed-refs` und legt `permissions.deny` an oder ergänzt es um 24 Edit-Regeln (13 Ordner mit `/**`, 11 Dateien ohne). Er bricht ab, ohne etwas zu ändern, wenn die Datei fehlt oder kein gültiges JSON ist. Ebenso, wenn `denyWrite` nicht genau die 30 bekannten Einträge hat, `allowWrite` nicht die vier bekannten oder einer der neuen Einträge schon da ist. Nach dem Schreiben prüft er alle anderen Schlüssel; die Einrückung wird neu gesetzt. Ist die Datei ein Symlink, ändert er das Ziel. Getestet am 26.11.2026 im Scratchpad an einer Nachbildung der Blöcke `permissions` und `sandbox` (die echte Datei zu kopieren verweigerte der Hook, protect_paths). Geprüft: Einfügen, zweiter Lauf mit Abbruch, fehlende Datei, kaputtes JSON, Symlink (Link bleibt, Ziel geändert), `denyWrite` oder `allowWrite` abweichend, Edit-Regel schon da (je Abbruch, Exit 1) und eine vorhandene `deny`-Liste (bleibt, 24 Regeln kommen dazu).

```sh
python3 -I - "$HOME/.claude/settings.json" <<'PY'
import json, os, shutil, sys, time
MAIN = "/Users/davidozturk/Projects/umbrarch"
DIRS = [".claude-plugin", ".github", ".monarch", "artifacts", "bench", "docs", "e2e", "plugin", "scripts", "src", "TestResults", "tests", "tools"]
FILES = [".DS_Store", ".editorconfig", ".gitignore", "CLAUDE.md", "Directory.Build.props", "Directory.Packages.props", "global.json", "LICENSE", "monarch.slnx", "nuget.config", "README.md"]
OLD_DENY = [MAIN + "/.git/" + p for p in ["HEAD", "index", "ORIG_HEAD", "FETCH_HEAD", "refs/heads/develop", "refs/heads/main"]] + [MAIN + "/" + e for e in sorted(DIRS + FILES, key=str.lower)]
OLD_ALLOW = ["/Users/davidozturk/.nuget/packages", "/Users/davidozturk/.local/share/NuGet/http-cache", "/Users/davidozturk/.dotnet/TelemetryStorageService", "/private/tmp"]
NEW_DENY_WRITE = [MAIN + "/.git/commondir", MAIN + "/.git/packed-refs"]
NEW_EDIT = ["Edit(/" + MAIN + "/" + d + "/**)" for d in DIRS] + ["Edit(/" + MAIN + "/" + f + ")" for f in FILES]
path = os.path.realpath(sys.argv[1])
if not os.path.isfile(path):
    sys.exit(f"Abbruch: {path} fehlt, nichts geändert")
with open(path, encoding="utf-8") as fh:
    raw = fh.read()
try:
    data = json.loads(raw)
except json.JSONDecodeError as e:
    sys.exit(f"Abbruch: {path} ist kein gültiges JSON ({e}), nichts geändert")
if not isinstance(data, dict):
    sys.exit("Abbruch: oberste Ebene ist kein Objekt, nichts geändert")
sb = data.get("sandbox")
fs = sb.get("filesystem") if isinstance(sb, dict) else None
if not isinstance(fs, dict) or not isinstance(fs.get("denyWrite"), list) or not isinstance(fs.get("allowWrite"), list):
    sys.exit('Abbruch: "sandbox.filesystem" mit denyWrite und allowWrite fehlt, nichts geändert')
if sorted(fs["denyWrite"], key=str.lower) != sorted(OLD_DENY, key=str.lower):
    sys.exit("Abbruch: denyWrite weicht vom erwarteten Stand (30 Einträge) ab, nichts geändert")
if sorted(fs["allowWrite"]) != sorted(OLD_ALLOW):
    sys.exit("Abbruch: allowWrite weicht vom erwarteten Stand ab, nichts geändert")
perms = data.get("permissions", {})
if not isinstance(perms, dict):
    sys.exit('Abbruch: "permissions" ist kein Objekt, nichts geändert')
deny = perms.get("deny", [])
if not isinstance(deny, list):
    sys.exit('Abbruch: "permissions.deny" ist keine Liste, nichts geändert')
dup = [e for e in NEW_EDIT if e in deny] + [e for e in NEW_DENY_WRITE if e in fs["denyWrite"]]
if dup:
    sys.exit(f"Abbruch: schon vorhanden: {dup}, nichts geändert")
backup = f"{path}.bak-{time.strftime('%Y%m%d-%H%M%S')}"
tmp = f"{path}.tmp-sandbox"
for p in (backup, tmp):
    if os.path.exists(p):
        sys.exit(f"Abbruch: {p} existiert schon, nichts geändert")
new = json.loads(raw)
nfs = new["sandbox"]["filesystem"]
nfs["denyWrite"] = nfs["denyWrite"] + NEW_DENY_WRITE
new.setdefault("permissions", {})
new["permissions"]["deny"] = deny + NEW_EDIT
shutil.copy2(path, backup)
with open(tmp, "w", encoding="utf-8") as fh:
    json.dump(new, fh, indent=2, ensure_ascii=False)
    fh.write("\n")
with open(tmp, encoding="utf-8") as fh:
    check = json.load(fh)
# Prüfung: nur denyWrite und permissions.deny haben sich geändert, und genau um die neuen Einträge.
def without(d):
    d = json.loads(json.dumps(d))
    d["sandbox"]["filesystem"].pop("denyWrite")
    d.get("permissions", {}).pop("deny", None)
    if d.get("permissions") == {}: d.pop("permissions")
    return d
ok = without(check) == without(data)
ok = ok and check["sandbox"]["filesystem"]["denyWrite"] == fs["denyWrite"] + NEW_DENY_WRITE
ok = ok and check["permissions"]["deny"] == deny + NEW_EDIT
if not ok:
    sys.exit(f"Abbruch: Prüfung fehlgeschlagen, {path} unverändert, Entwurf liegt in {tmp}")
os.chmod(tmp, os.stat(path).st_mode & 0o777)
os.replace(tmp, path)
print(f"Sicherung: {backup}")
print(f"denyWrite +{len(NEW_DENY_WRITE)} (jetzt {len(check['sandbox']['filesystem']['denyWrite'])}), permissions.deny +{len(NEW_EDIT)} (jetzt {len(check['permissions']['deny'])})")
print("Alle anderen Schlüssel unverändert (Werte geprüft, Einrückung neu).")
PY
```

**Proben nach der Aktivierung** (Definition des Owners vom 26.11.2026, wörtlich: „f) <main>/.git/commondir anlegen muss scheitern, g) ": >> <main>/.git/packed-refs" muss scheitern, h) Edit-Werkzeug auf <main>/README.md muss verweigert werden, beide Male develop vorher/nachher.“): Ergebnisse folgen nach der Aktivierung.

### Schicht 2: git-Hook reference-transaction

Owner-Vorgabe vom 25.11.2026, wörtlich:

> - Nur im Zustand "prepared" entscheiden, sonst exit 0.
> - CLAUDECODE leer → exit 0 (mein Terminal bleibt unberührt).
> - Sonst verweigern, wenn git-dir == git-common-dir (Haupt-Checkout) oder eine Ref refs/heads/develop oder refs/heads/main ist. Meldung mit Verweis auf ADR-0020 §1.
> - Der bestehende .git/hooks/pre-commit (gitleaks) bleibt unverändert.
> - Installation in .git/hooks macht NICHT der Agent. Die Sandbox sperrt das ohnehin. Gib mir einen Befehl, den ich selbst im Terminal ausführe, mit Prüfung, dass nichts Vorhandenes überschrieben wird.

Die dritte Zeile ersetzt der Owner-Entscheid nach dem Bericht zu Q-0201, wörtlich:

> Meine Vorgabe "verweigern, wenn git-dir == git-common-dir" war zu grob, weil sie neue Gate-Worktrees blockiert. Neu:
> - Mit CLAUDECODE nicht leer immer verweigern: refs/heads/develop und refs/heads/main (Schreibweise egal, wie jetzt).
> - Im Haupt-Checkout (git-dir == git-common-dir) zusätzlich verweigern: HEAD und die Branch, auf die HEAD des Haupt-Checkouts zeigt.
> - Erlaubt: neue oder geänderte andere Refs, auch aus dem Haupt-Checkout (git worktree add -b monarch/<G>-… origin/develop), fetch.

Umgesetzt in Q-0202, mit einer Abweichung (unten, „Abweichung für `worktree add`“).

**Was git dem Hook gibt** (git 2.54, Apple Git-157; Beleg: Debug-Ausgabe des Knights im Hook, danach entfernt). Der Ref-Name steht immer in der dritten Spalte:
- `git checkout other` im Haupt-Checkout: `0000… ref:refs/heads/other HEAD`;
- `git checkout --detach`: `0000… <sha> HEAD`;
- `git worktree add <pfad> -b monarch/G-9-x origin/develop` aus dem Haupt-Checkout: `0000… <sha> refs/heads/monarch/G-9-x` und `0000… ref:refs/heads/monarch/G-9-x HEAD`.

**Abweichung für `worktree add`.** Owner-Entscheid vom 26.11.2026, wörtlich: „Lock-Lösung für worktree add: so lassen. Schicht 1 sperrt <main>/.git/HEAD zusätzlich. Der Warden prüft sie gezielt.“ `worktree add` schreibt das HEAD der neuen Worktree aus dem Haupt-Checkout heraus. Der Hook sieht dafür dieselbe Zeile wie bei `git checkout` und dasselbe git-dir. Die Regel „HEAD im Haupt-Checkout verweigern“ würde darum auch `worktree add -b` verweigern, das der Owner ausdrücklich erlaubt. Unterscheiden lassen sich die Fälle nur an den Lock-Dateien. Bei `worktree add` fehlt `.git/HEAD.lock`, dafür liegt `.git/worktrees/<name>/HEAD.lock` vor (Beobachtung des Knights). Ändert git das HEAD des Haupt-Checkouts, liegt `.git/HEAD.lock` vor. Das belegt der Test `Checkout_in_the_main_checkout_is_refused_even_with_a_planted_worktree_HEAD_lock`: Trotz einer vorgetäuschten `.git/worktrees/G-1/HEAD.lock` bleibt `checkout` im Haupt-Checkout verweigert. Das Skript erlaubt eine HEAD-Zeile im Haupt-Checkout darum nur, wenn `.git/HEAD.lock` fehlt und eine `.git/worktrees/*/HEAD.lock` existiert. Sonst verweigert es.
- Das hängt am Dateiformat `files` für Refs. Mit `reftable` gibt es diese Lock-Dateien nicht. Für `reftable` müsste die Konfiguration des Repos geändert werden, und `config` im gemeinsamen `.git` sperrt die Sandbox.
- Strengere Alternative: HEAD im Haupt-Checkout immer verweigern. Dann geht `worktree add -b` nur aus einer bestehenden Worktree (`git -C <wt> worktree add …`, Test `Documents_worktree_add_with_new_branch_from_a_worktree_with_CLAUDECODE`). Das wäre auch der Weg für `gate open` aus einer Gate-Worktree.

**Skript.** `plugin/git-hooks/reference-transaction`, reines sh ohne monarch-Binary. Kann es git-dir oder git-common-dir nicht bestimmen, verweigert es (Vorschlag, nach dem Grundsatz „unklar heißt nicht erlaubt“). Die Branch des Haupt-Checkouts liest es mit `git symbolic-ref -q HEAD`; bei detached HEAD gilt nur `HEAD`. Es wirkt nur, wenn `core.hooksPath` nicht gesetzt ist. Am 25.11.2026 war es nirgends gesetzt (`git config --show-origin --get-all core.hooksPath`: keine Ausgabe, Exit 1). Die Namen `refs/heads/develop` und `refs/heads/main` vergleicht es ohne Beachtung der Schreibweise (Vorschlag), denn auf macOS ist `refs/heads/Develop` dieselbe Datei wie `refs/heads/develop`.

**Installation (Owner, im eigenen Terminal, im Haupt-Checkout).** `<quelle>` ist das Skript aus einem Checkout mit Q-0202, etwa `.claude/worktrees/G-0130/plugin/git-hooks/reference-transaction` oder nach dem Merge `plugin/git-hooks/reference-transaction`. Der Befehl bricht ab, wenn `core.hooksPath` gesetzt ist oder schon ein Hook dieses Namens existiert:

```sh
hook="$(git rev-parse --git-common-dir)/hooks/reference-transaction"; src=<quelle>
if [ -n "$(git config --get core.hooksPath)" ]; then echo "Abbruch: core.hooksPath ist gesetzt"; elif [ -e "$hook" ]; then echo "Abbruch: $hook existiert"; else install -m 0755 "$src" "$hook" && cmp "$src" "$hook" && ls -l "$hook"; fi
```

**Tests** (`tests/Monarch.Cli.Tests/ReferenceTransactionHookTests.cs`, temporäre Repos mit Remote und verknüpfter Worktree, Hook nach `.git/hooks` kopiert):
- Mit `CLAUDECODE=1` verweigert:
  - im Haupt-Checkout: Commit; `checkout <branch>`, `switch <branch>`, `checkout --detach` und `checkout -b <neu>`, HEAD bleibt jeweils unverändert; `checkout <branch>` auch mit vorgetäuschter Worktree-Lock-Datei (Absicherung der Main-Session, nicht rot); `update-ref` auf die ausgecheckte Branch, auch in anderer Schreibweise;
  - aus der Worktree: `branch -f develop` und `branch -D develop` (gepackt); `update-ref` auf `refs/heads/Develop`, `refs/heads/MAIN` und `refs/HEADS/develop`;
  - `update-ref --stdin` mit einer erlaubten Ref und `refs/heads/develop` in einer Transaktion: ganz abgelehnt, die erlaubte Ref entsteht nicht, develop bleibt unverändert.
- Mit `CLAUDECODE=1` erlaubt:
  - in der Worktree: Commit und `fetch`;
  - aus dem Haupt-Checkout: `worktree add <pfad> -b monarch/G-9-x origin/develop` (Branch und Worktree entstehen), `branch <neu>` und `update-ref` auf eine neue Branch;
  - aus einer Worktree: `update-ref` auf die Branch des Haupt-Checkouts, wenn sie nicht develop oder main ist (Test `Documents_…`, so steht es in der Owner-Regel).
- Ohne `CLAUDECODE` erlaubt: Commit im Haupt-Checkout, `branch -f develop`, `branch -D develop` (gepackt).
- Rot vor der Änderung des Skripts (Bericht des Knights): `Worktree_add_with_new_branch_from_the_main_checkout_is_allowed`, `Branch_create_in_the_main_checkout_is_allowed` und `Update_ref_with_a_new_branch_in_the_main_checkout_is_allowed`. Die übrigen neuen Tests waren schon mit der alten, strengeren Regel grün.

**Folgen für Abläufe** (Beleg: Tests in derselben Datei, git 2.54 auf macOS):
- `git worktree remove <wt>` aus dem Haupt-Checkout bleibt erlaubt. Das braucht `gate cleanup` (`GateCleanupCommands.cs:142`).
- `gate merge` ändert lokal weder develop noch main. Den Merge macht GitHub (`GitHubPulls.cs:124`, `PUT …/pulls/{n}/merge`).

### Gate öffnen mit Sandbox

Owner-Entscheid vom 26.11.2026, wörtlich: „Gate öffnen: Übergang wie empfohlen. Ich lege Gate-Worktrees im eigenen Terminal an (git -C ~/Projects/umbrarch worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop) und starte die Session dort. Standardregel 2 in plugin/skills/regeln/SKILL.md und der Deny-Text in ProtectMainCheckoutRule.cs entsprechend anpassen. monarch gate open später als eigenes Issue.“ Umgesetzt: der Deny-Text in Q-0203. Standardregel 2 folgt in Q-0204, weil die Checkliste in `tests/Monarch.Cli.Tests/RegelnTextTests.cs:43` den Schritt „EnterWorktree“ verlangt und diese Datei nicht im Scope von Q-0203 liegt. Der Rest dieses Abschnitts ist die Vorlage für den Entscheid.

**Heute.** Standardregel 2 (`plugin/skills/regeln/SKILL.md:10`): „Anlegen: `git worktree add .claude/worktrees/<G-xxxx> -b monarch/G-xxxx-<slug> origin/develop` … Danach **EnterWorktree mit `path`**.“ Danach folgt `gate carry` (`SKILL.md:20`, Checkliste `:63`). Der Pfad ist relativ zur Wurzel des Haupt-Checkouts. Die Session startet also im Haupt-Checkout, legt die Worktree selbst an und wechselt mit EnterWorktree hinein. Bis Q-0203 nannte der Deny-Text von `protect_main_checkout` denselben Befehl (`ProtectMainCheckoutRule.cs:8-10`). Einen Befehl `monarch gate open` gibt es nicht (`MonarchApp.cs`, keine Fundstelle).

**Default des Owners**, wörtlich: „Sessions starten immer in einem Gate-Worktree; ein neues Worktree legt monarch gate open an, dafür allowWrite nur auf /Users/davidozturk/Projects/umbrarch/.claude/worktrees.“

Bewertung der Session (Vorschlag):
- **Trägt nach Doku und Hook-Regel:** Aus einer Gate-Worktree darf die Sandbox das gemeinsame `.git` schreiben (Doku oben). Die neue Branch `monarch/<G>-…` erlaubt Schicht 2, auch aus dem Haupt-Checkout. Die neue Worktree unter `.claude/worktrees` deckt `allowWrite` ab.
- **Braucht neuen Code:** `monarch gate open <G> <slug>` gibt es nicht. Ein Bash-Befehl `git -C <main> worktree add …` geht aus einer Session mit Worktree-Isolation nicht. Claude Code verweigerte am 25.11.2026: „a worktree-isolated session's git operations must target its own worktree“. Den git-Aufruf muss darum das monarch-Binary machen.
- **Zu prüfen:** `git worktree add -b … origin/develop` trägt die Upstream-Branch normalerweise in `.git/config` ein. Die Sandbox sperrt `config` im gemeinsamen `.git`. `gate open` sollte darum `--no-track` nehmen und mit Refspec pushen. Das ist nicht getestet.
- **Folge von `allowWrite` auf `.claude/worktrees`:** Jede Session, auch in anderen Projekten, darf in alle Gate-Worktrees von umbrarch schreiben.
- **Ohne neuen Code:** Der Owner legt jede Gate-Worktree im eigenen Terminal an (`git -C ~/Projects/umbrarch worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop`) und startet die Session dort. Dann braucht es kein `allowWrite` auf `.claude/worktrees`.
- Empfehlung: `gate open` erst bauen, wenn der Test nach der Aktivierung `--no-track` und das gemeinsame `.git` bestätigt hat. Bis dahin legt der Owner die Worktree an, und `allowWrite` bleibt ohne `.claude/worktrees`.

### Session im Haupt-Checkout

Owner-Entscheid vom 26.11.2026, wörtlich: „Session im Haupt-Checkout: Option C (denyWrite auf die Liste der obersten Einträge des Haupt-Checkouts, ohne .git und .claude), erst nach einem Test der Pfad-Semantik in dieser Session. B nicht bauen. Ändern der User-Settings mache ich; du gibst mir den Befehl im selben Stil (Sicherung, Abbruch, nur filesystem.denyWrite ergänzen).“ Der Test der Pfad-Semantik steht unter „Test nach der Aktivierung“. Den Befehl legt die Session dem Owner im Bericht vor. Der Rest dieses Abschnitts ist die Vorlage für den Entscheid.

„B nicht bauen“ ersetzt der Owner-Entscheid 3 vom 26.11.2026, wörtlich im Abschnitt „Dritte Runde“. Zusammenfassung: B jetzt bauen, über `CLAUDE_PROJECT_DIR` statt über einen Zustand aus SessionStart; C bleibt zusätzlich. Umgesetzt in Q-0209.

Startet eine Session im Haupt-Checkout, kann Bash dessen Arbeitsbaum schreiben. Das ist die einzige Lage, in der das geht (Schicht 1 oben). Ein SessionStart-Hook kann die Session laut Hooks-Doku nicht sperren; laut Zusammenfassung der Doku hat er „No blocking or decision control“. Ein PreToolUse-Hook kann einen Aufruf mit Grund verweigern (`"permissionDecision": "deny"`, Abschnitt „How a hook resolves“). Optionen:
- **A. Nur das Briefing** (heute eine Warnung, Q-0201). Vorteil: kein Code. Nachteil: Es ist nur ein Hinweis.
- **B. Harte Sperre im PreToolUse-Hook nach der Session-Wurzel.** Der SessionStart-Hook merkt sich je `session_id` das Startverzeichnis. Ist das ein Haupt-Checkout, verweigert PreToolUse jeden Bash-Aufruf und jedes Datei-Werkzeug mit dem Grund „Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu“. Erlaubt bleiben höchstens feste, wörtlich gelistete Befehle wie `~/.monarch/bin/monarch status`. Vorteile: kein Shell-Parsing, kein cwd-Modell; `cd` in eine Worktree hebt die Sperre nicht auf, weil die Sandbox den Startordner weiter freigibt. Nachteile: Es ist Schicht 3; ohne Plugin oder Hook gibt es keine Sperre. Fortgesetzte Sessions brauchen den Eintrag erneut (Annahme: SessionStart läuft auch beim Fortsetzen; nicht geprüft). Wer sich in den Haupt-Checkout verirrt, muss die Session neu starten.
- **C. Sandbox-Sperre für den Arbeitsbaum.** `denyWrite` auf die Einträge der obersten Ebene des Haupt-Checkouts, entweder per Glob `/Users/davidozturk/Projects/umbrarch/*` oder als Liste (`src`, `tests`, `docs`, `plugin`, `.monarch`, `.github` und so weiter). Vorteile: Die OS-Ebene passt zum Bedrohungsmodell und gilt auch für eine Session im Haupt-Checkout. Nachteile: Ob `*` Punkt-Einträge (`.git`, `.claude`) trifft und wie `allowWrite` gegen `denyWrite` gewinnt, sagt die Doku nicht. Mit `.git` wären auch die Worktrees blockiert. Die Liste muss bei neuen Einträgen der obersten Ebene mitwachsen.
- **D. Startsperre in der Shell des Owners** (Funktion `claude` in `~/.zshrc`, die im Haupt-Checkout abbricht). Vorteil: Die Sperre greift vor dem Start. Nachteile: Sie gilt nur im eigenen Terminal, der volle Pfad umgeht sie, und sie liegt außerhalb des Repos.
- Empfehlung: C mit der Liste (OS-Ebene, dokumentierte Pfad-Semantik), dazu B als Frühwarnung. C erst nach einem Test der Pfade nach der Aktivierung.

### Schicht 3: PreToolUse-Hook (Frühwarnung)

Owner-Vorgabe vom 25.11.2026, wörtlich: „Bash-cwd-Modell einfrieren, keine Änderung an der cd-Logik.“, „Neu nur eine feste Sperrliste im Bash-Text: hooksPath, GIT_CONFIG, CLAUDECODE (Groß-/Kleinschreibung egal), mit Tests.“ und „Edit/Write/NotebookEdit: prüfen, dass der Pfad per realpath gegen den Haupt-Checkout geprüft wird (Symlinks). Wenn nicht, ergänzen.“

Umsetzung in Q-0201 (`src/Monarch.Policy/ProtectMainCheckoutRule.cs`):
- **Sperrliste:** Ein Bash-Befehl, dessen Text `hooksPath`, `GIT_CONFIG` oder `CLAUDECODE` enthält (Teilstring, Schreibweise egal), wird verweigert, egal wo das cwd liegt. Der Grund nennt den Weg: Suchen mit dem Grep-Werkzeug, Texte über eine Datei (`git commit -F <datei>`, `gh … --body-file <datei>`).
- **Datei-Werkzeuge:** Für Edit, Write, MultiEdit und NotebookEdit löst die Regel den Zielpfad über Symlinks auf (`PathResolver.Resolve`; fehlende Teile bleiben, wie sie sind). Dann sucht sie vom Zielordner aufwärts das nächste `.git`. Ist das ein Haupt-Checkout, verweigert sie. Pfade in einer Worktree und außerhalb jedes Repos bleiben erlaubt. Die Datei-Werkzeuge laufen nicht in der Sandbox (Schicht 1). Gegen Schreiben in den Haupt-Checkout per Edit oder Write steht darum nur diese Regel, neben den Permission-Regeln von Claude Code.
- **SessionStart:** Läuft die Session im Haupt-Checkout, warnt das Briefing (`src/Monarch.Cli/HookRunner.cs`). Seit Q-0208 mit derselben Erkennung wie die Regel (unten, „Was ein Haupt-Checkout ist“).
- **Startwurzel (Option B, Q-0209):** Ist realpath von `CLAUDE_PROJECT_DIR` ein Haupt-Checkout oder ein Ordner darin, verweigert die Regel Bash, Edit, Write, MultiEdit und NotebookEdit mit dem Grund „Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu“. Read, Grep, Glob und die übrigen Werkzeuge bleiben erlaubt. Das gilt auch, wenn das cwd außerhalb jedes erweckten Projekts liegt; dann schreibt der Hook kein Ledger.

Die folgenden Absätze beschreiben die Regel `protect_main_checkout` für Bash im eingefrorenen Stand nach Q-0200.

**Was zählt.** Ein Bash-Segment mit dem Wort `git` und einem dieser Unterbefehle: `checkout`, `switch`, `reset`, `merge`, `pull`, `commit`, `rebase`, `stash`. Den Unterbefehl bestimmt `BashAnalysis.GitSubcommand` wie bei `protect_branches`; globale Optionen wie `-c` oder `--no-pager` davor zählen nicht. `git stash list` und `git stash show` lesen nur und zählen nicht (Vorschlag: Lesart von „schreibende Git-Befehle“).

**Wo der Befehl läuft.** Die Verzeichnisse vor dem Segment (cwd, `cd`, `pushd`, `popd` aus dem cwd-Modell), danach jedes `-C <dir>` der Reihe nach. Ein `env -C <dir>` oder `env --chdir=<dir>` direkt vor `git` zählt wie `git -C <dir>`, auch in getopt-Schreibweise (`-C<dir>`, gebündelt `-iC <dir>`, gekürzt `--ch <dir>`); je `env` gilt das letzte `-C`. Vor `sh -c`, `bash -c` und einer Shell, die einen Heredoc liest, gilt `<dir>` als Startverzeichnis ihres Codes; bei `… <<EOF | env -C <dir> sh` zählt das Verzeichnis der Shell hinter der Pipe, nicht das des Befehls davor. Quelle: Warden-Code-Runde und Delta-Runde zu Q-0193 (`env -C`, getopt-Formen, `sh -c`); Kürzung, Heredoc und Pipe-Fall aus der Prüfung des Kritikers. Der Wert von `env -C` gilt wörtlich, auch wenn er mit `-` beginnt, und nach `cd --` gilt das nächste Wort wörtlich (schmale Prüfung Q-0193, `#issuecomment-7083652257`). Unklare Verzeichniswechsel regelt der Owner-Entscheid unten. Die Regel prüft eine Vereinigung von Kandidaten und verweigert, sobald einer davon ein Haupt-Checkout ist:
- das Git-Verzeichnis je Basis über `BranchGuard.FindGitDir`. Basen sind die Verzeichnisse aus dem cwd-Modell (cwd, `cd`, `pushd`, `popd`). Jedes `-C <dir>` ersetzt die Basen der Reihe nach, wie bei `FindRepos` (`ProtectBranchesRule.cs:215-223`), denn Git arbeitet dann in `<dir>` und nicht im cwd. Lässt sich ein `-C` nicht auflösen, bleiben die bisherigen Basen stehen (`:223`), mit cwd im Haupt-Checkout also ein Deny;
- jedes ausdrückliche Git-Verzeichnis aus `--git-dir` im Segment oder `GIT_DIR` aus irgendeinem Segment;
- jeden ausdrücklichen Arbeitsbaum aus `--work-tree` im Segment oder `GIT_WORK_TREE` aus irgendeinem Segment.

Anders als bei `FindRepos` (`ProtectBranchesRule.cs:205-245`, dort ersetzen `--git-dir` und `GIT_DIR` die Suche, `:226-241`, und `--work-tree` wird nicht ausgewertet) ersetzen `--git-dir`, `GIT_DIR`, `--work-tree` und `GIT_WORK_TREE` hier die Suche über die Basen nicht, sie kommen als Kandidaten hinzu. Relative Werte gelten wie bei `FindRepos` gegen die Basen nach `-C` (`:236-238`). So bleibt `git --work-tree=/tmp/x checkout main` im Haupt-Checkout ein Deny, denn Git nimmt das Git-Verzeichnis dann aus dem cwd. Dasselbe gilt für `GIT_DIR=/tmp/y/.git git status; git checkout main`. Folge der Vereinigung: Ein Befehl mit `GIT_DIR` einer Worktree, aber cwd im Haupt-Checkout, wird ebenfalls verweigert (streng). Erlaubt bleibt `git -C <Worktree> commit` mit cwd im Haupt-Checkout, denn `-C` ersetzt die Basis. Das braucht das Aufräumen von Hand nach Standardregel 2, bis `gate cleanup` es übernimmt (Abschnitt 6).

**Unklare Verzeichniswechsel.** Nach der Warden-Schlussrunde (`#issuecomment-7161665432`, major 1 und 2) gilt der Owner-Entscheid vom 25.11.2026, wörtlich: „Streng fixen, aber nach dem Grundsatz „unklar heißt nicht erlaubt“, ohne jede Schreibweise einzeln nachzubauen:
- cd/pushd mit einer Option außer -L/-P (auch zsh -q, -s): das cwd-Modell behält das alte Verzeichnis und das Ziel (Vereinigung).
- Ziel existiert nicht: das alte Verzeichnis und das Ziel (Vereinigung). Dafür braucht es keinen Lexer für &&.
- cd mit zwei Operanden, pushd +N/-N und popd +N/-N: protect_main_checkout verweigert jeden schreibenden git-Befehl, der in diesem Befehl danach kommt. Grund: „Arbeitsverzeichnis nicht eindeutig (cd/pushd-Variante); nutze git -C <Gate-Worktree>.““
Umgesetzt in Q-0200.

Die vier major der Delta-Runde zu Q-0200 (`#issuecomment-7167800109`) schließt der Parser nicht mehr (Bedrohungsmodell oben): `cd -` nach einem `cd`, das scheitern kann; zsh-Wörter mit `-` als Operanden; `--` vor `+N`/`-N`; zwei Operanden aus einem Ordner ohne Projekt. Ref-Änderungen an develop, main und, im Haupt-Checkout, an HEAD und dessen Branch verweigert Schicht 2. Mit der Sandbox in den User-Settings greift Schicht 1 nach der Aktivierung in jeder Session. Per Bash kann den Arbeitsbaum des Haupt-Checkouts dann nur eine Session schreiben, die im Haupt-Checkout läuft; Edit und Write laufen nicht in der Sandbox und stoßen auf Schicht 3. Owner-Entscheid vom 25.11.2026, wörtlich: „Punkt 4 ist keine Grenze mehr.“ Lesart der Session: gemeint ist der vierte Fall, nach Frage 2 im Kurzbericht nach Q-0200 („Punkt 4“ als Grenze führen).

Lesart der Session (Vorschlag), über den Wortlaut hinaus: `cd +N`/`cd -N` (zsh) ist dieselbe Stapelwahl wie `pushd +N` und zählt mit; `pushd` mit zwei Operanden zählt wie `cd` mit zwei Operanden; „jeden schreibenden git-Befehl“ heißt die acht Befehle dieser Regel, auch mit `git -C <Gate-Worktree>` im selben Bash-Befehl, und die Markierung gilt bis zum Ende des Bash-Befehls, auch nach einer Subshell.

**Was ein Haupt-Checkout ist** (Stand Q-0208). Das gefundene Git-Verzeichnis heißt `.git` und ist ein Verzeichnis. Der Ordner darüber enthält ein Verzeichnis `.monarch` (erwecktes Projekt). git-dir und git-common-dir sind nach realpath gleich, wie im git-Hook; anders als dort ohne Beachtung der Schreibweise (strenger, `ProtectMainCheckoutRule.cs`, `OrdinalIgnoreCase`). Das git-common-dir steht in der Datei `commondir`, relativ zum git-dir; fehlt sie, ist es das git-dir. Ist `commondir` leer, unlesbar oder zeigt es auf einen Ordner, den es nicht gibt, zählt es als Haupt-Checkout. Bis Q-0208 hieß „Haupt-Checkout“: keine Datei `commondir`. Eine Datei `commondir` mit Ziel `.` schaltete damit die Regel ab (Warden-Befund 1, `#issuecomment-7185290617`). Für `--work-tree` und `GIT_WORK_TREE`: `BranchGuard.FindGitDir(<Arbeitsbaum>)` und derselbe Test. So zählt auch ein Unterordner des Haupt-Checkouts, etwa `git --work-tree=<Haupt-Checkout>/src checkout -f -- .` aus einer Worktree. Ein Arbeitsbaum ohne Git-Verzeichnis darüber (etwa `/tmp/x`) ergibt keinen Kandidaten. Ein Worktree unter `.claude/worktrees/` hat eine `.git`-Datei. Sein Git-Verzeichnis liegt unter `.git/worktrees/<name>` und zählt nie.

**Entscheidung.** Deny mit Regel `protect_main_checkout` und dem Weg: „Der Haupt-Checkout bleibt unverändert (Standardregel 2). Arbeite in der Gate-Worktree. Eine neue legt der Owner im eigenen Terminal an (`git -C <Haupt-Checkout> worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop`) und startet die Session dort.“ (Text nach dem Owner-Entscheid „Gate öffnen“ vom 26.11.2026, Q-0203.) Das gilt für Main und Shadows gleich. Die Regel läuft in `Evaluate` und in `EvaluateProtection`, also auch, wenn der Haupt-Checkout nur Ziel eines Befehls ist, etwa `git -C ~/Projects/Noelia checkout main` aus einer anderen Session.

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

**Wiederholung im Repo (23.11.2026, `tools/shadow-costs.py`, Commit `2f305e5`).** Befehl: `python3 tools/shadow-costs.py <umbrarch> <Noelia> <workertransfer>`; 15 641 Ereignisse, 175 Läufe, 34 gezählt, p90 nach Nearest-Rank. Es gilt diese Tabelle:

| Shadow (Modell laut `plugin/agents/*.md`) | n | 5h Median | 5h p90 | 5h Max | 7d p90 |
|---|---|---|---|---|---|
| warden (opus) | 17 | 9 | 18 | 18 | 3 |
| knight (sonnet) | 16 | 4.5 | 12 | 19 | 2 |
| scout (sonnet) | 1 | – | – | – | – |

Der frühere Höchstwert 36 (Warden, 04.11.2026) zählt nach den Kriterien nicht: Der Snapshot danach kam 297 s nach dem Ende.

Für kritiker und fremde Agenten (`general-purpose` u. a.) gibt es keinen sauberen Lauf.

**(c) Wird der Stand im langen Lauf frisch?** Ja. Beleg (Noelia, 21.11.2026): Die Hauptsession benutzt um 21:13:24 zuletzt ein Werkzeug (Session-Ledger `be145c6d…`, seq 179), der Warden endet um 21:29:30 (seq 209). Dazwischen ruft der Warden selbst `monarch mana` auf: `cli-20261121`, seq 30 um 21:21:17 mit 47 % und seq 31 um 21:25:48 mit 50 %, beide `stale: false`; vor dem Start (seq 29, 21:13:20) waren es 37 %. Ohne Aktivität der Hauptsession kann ein frischer Wert nach mehr als 10 Minuten nur aus einer Statusline während des Shadow-Laufs stammen (`ManaStore.StaleAfter`, `State.cs:58`).

### (a) Prüfung vor dem Start

- **Matcher (Vorschlag):** Der PreToolUse-Matcher in `plugin/hooks/hooks.json:16` bekommt `Agent|Task|Glob|WebFetch|WebSearch` dazu. Damit sieht der Hook den Start eines Shadows, und die Bremse in (b) erfasst alle Werkzeuge, die Arbeit kosten. Bewusst nicht erfasst bleibt das Übergabe-Werkzeug des Subagenten (in dieser Umgebung `SubagentHandback`): Nur darüber erreicht der unvollständige Stand die Hauptsession, auch der hook_error-Pfad darf es nicht sperren. Ebenso nicht erfasst bleiben reine UI-Werkzeuge wie TodoWrite. Für Glob, WebFetch, WebSearch, Agent und Task greift heute keine Regel. Ein Test belegt, dass sie ohne Mana-Befund erlaubt bleiben.
- **Erkennen des Shadows:** Werkzeugname `Agent` oder `Task`. `tool_input.subagent_type` zählt nur mit dem Teil nach dem letzten `:` (wie `ToolCallPolicy.AgentName`). `tool_input.model` darf `opus`, `sonnet`, `haiku`, eine volle Modell-ID oder `inherit` sein. Die neuen Felder liest der Hook nachsichtig (`JsonElement`): Ein Wert, der kein Text ist, zählt als fehlend und löst keinen hook_error aus.
- **Modellklasse des Shadows:** `tool_input.model` (Familie aus dem Namen; `inherit` zählt als fehlend), sonst das Modell aus der Agent-Definition für die Umbrarch-Agenten (warden, kritiker, strategist: opus; knight, scout: sonnet; ein Test gleicht das mit `plugin/agents/*.md` ab), sonst das Modell der Session aus `mana.json`, sonst Opus (`ModelClasses.Of`).
- **Erwartete Kosten (5h, 7d):** warden/opus 18 und 3, knight/sonnet 12 und 2 (Wiederholung 23.11.2026). Für jede andere Kombination gilt der konservative Default 18 und 3, der höchste belegte p90. Vorschlag: p90, und einen eigenen Wert nur ab n ≥ 5. Begründung mit dem Fall aus der Sammlung 6b (Noelia, 21.11.2026, `cli-20261121.e68aa931.jsonl` seq 5 um 00:49:04 mit 77 %, seq 6 um 01:11:40 mit 95 %): Der Warden startete bei 77 % (Grenze 90, Noelia `.monarch/system.yaml:8`). Mit dem Median (10) hätte der Hook ihn gestartet (87 %), er endete bei 95 %. Mit p90 (18) verweigert der Hook (95 %).
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
- Lesart (Vorschlag, Q-0195): Schritt 2 greift nur, wenn jedes Fenster, das nicht passt, schon zurückgesetzt ist, und es zählt der späteste dieser Resets. Ein noch nicht zurückgesetztes Fenster über der Grenze ändert auch ein frischer Stand nicht (dann Schritt 3 oder 4). `mana --json` aus der DoD gibt es nicht (`MonarchApp.cs:189-191`); die Meldung steht stattdessen in `status --json` (`guardMessage`) und in `mana_forecast` (`message`).
- Vorschlag für die Obergrenze: `ManaStore.StaleAfter` (10 Minuten, ADR-0004), keine neue Zahl. Die Vorgabe des Owners nennt keinen Wert. Vorschlag für die Zeit danach: RecapAndStop statt heute PauseWithoutRecap. Das ist eine Verhaltensänderung: Kommt nach 10 Minuten kein frischer Stand (etwa ohne Statusline), sichert die Session ihren Stand im Recap, statt ohne Recap zu pausieren. Owner-Entscheid vom 25.11.2026 dazu, wörtlich: „So lassen.“
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

- `monarch gate cleanup <G>` (nur Main), aus dem Haupt-Checkout nach ExitWorktree. Mit der Sandbox führt ihn vorerst der Owner im eigenen Terminal aus (Owner-Entscheid vom 26.11.2026, §1 „Zweite Runde nach der Aktivierung“); dort läuft kein Hook. Der Befehl findet die Worktree über `git worktree list --porcelain` am Branch `monarch/<G>-…`. Sie muss ein verknüpfter Worktree unter `.claude/worktrees/` sein.
- Ablehnung, wenn das cwd in der Worktree liegt (der Hook würde dort weiter Ledger-Zeilen schreiben), wenn `ledger verify` in der Worktree Befunde hat oder wenn für das Gate nach dem letzten `GateMerged` kein `IntegrationRunChecked` mit `green` vorliegt (Standardregel 2, Schritt 1). Ohne `GateMerged` kommt so nie ein `[skip ci]` vor dem Merge auf den Branch.
- Ablauf, alles per `git -C`: Prüfung auf gestagte Dateien außerhalb `.monarch` (`git -C <wt> diff --cached --name-only --no-renames`, Abbruch bei Exit ≠ 0 oder bei einem Treffer), dann `GateCleanedUp` in die Worktree schreiben, `git -C <wt> add .monarch`, `git -C <wt> commit -m "<G>: Monarch-Stand nach Merge [skip ci]" -- .monarch` (der pre-commit-Hook läuft; schlägt er an, stoppt der Befehl und meldet den Fund), `git -C <wt> push origin monarch/<G>-…` (ausdrücklicher Refspec, nur der Gate-Branch), `git -C <haupt> worktree remove <wt>` ohne `--force`.
- Verweigert git das Entfernen, nennt Monarch Grund und Dateien (`git -C <wt> status --porcelain`) und endet mit Exit 1. Kein Text schlägt `--force` vor.
- Fehlerpfade (Vorschlag): Scheitert der Commit (etwa am pre-commit-Hook), wird nichts gepusht und nichts entfernt. Scheitert der Push, wird nichts entfernt. Ein zweiter Aufruf setzt fort: Ohne neue Änderungen entfällt der Commit, der Push ist ohne Wirkung, wenn origin schon auf dem Stand ist. `git add` nimmt nur `.monarch` (Vorschlag), weil nach dem Merge nur der Monarch-Stand offen sein darf; andere Änderungen lassen `worktree remove` scheitern, und Monarch nennt sie.
- Prüfung und Reihenfolge nach der Warden-Schlussrunde (`#issuecomment-7161665432`, major 3), Owner-Entscheid vom 25.11.2026, wörtlich: „Fix wie empfohlen. git diff --cached --name-only --no-renames mit Prüfung des Exit-Codes, dazu Commit mit Pfadangabe (git commit -m … -- .monarch). GateCleanedUp erst nach der Prüfung schreiben.“ Umgesetzt in Q-0200.

## Nur durch den Main

`gate cleanup`, `gate carry` und `gate move-quest` verweigert der Hook jedem Shadow, wie heute `gate merge|verdict|check` (`ProtectBranchesRule.cs:41-56`). Die Liste in `GateOnlyMain` bekommt die drei Unterbefehle dazu (Q-0197).
- Der Bash-Aufruf selbst läuft im Haupt-Checkout. Seine Hook-Zeilen landen dort (bestehende Lücke: untracked Ledger-Dateien im Haupt-Checkout). Solange der Owner `gate cleanup` im eigenen Terminal ausführt (§1 „Zweite Runde nach der Aktivierung“), läuft dabei kein Hook und es entstehen keine Hook-Zeilen.

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

1. Weitere schreibende Git-Befehle im Haupt-Checkout sperren (`add`, `restore`, `clean`, `cherry-pick`, `revert`, `am`, `apply`, `mv`, `rm`, `branch -f`, `tag`, `update-ref`)? Owner-Entscheid vom 25.11.2026, wörtlich: „Nicht in diesem Gate. Als eigener Punkt in #164.“
2. Status direkt nach dem Merge auf develop statt mit dem nächsten Gate: Das braucht eine Ausnahme im GitHub-Branchschutz von develop. Owner-Entscheid vom 25.11.2026, wörtlich: „Nein.“
3. Laufende Neuberechnung der Shadow-Kosten aus neuen Läufen (Issue für v0.2). Owner-Entscheid vom 25.11.2026, wörtlich: „Issue, wie vorgeschlagen.“ Angelegt als #165.
4. TLS unter Seatbelt (Vorschlag, Beleg unter „Test nach der Aktivierung“): `sandbox.enableWeakerNetworkIsolation: true` statt `excludedCommands`. Laut settings-reference: „Let sandboxed commands on macOS reach the system TLS trust service, `com.apple.trustd.agent`.“ und „This reduces security by opening a potential data exfiltration path through the trust service.“ Zu `excludedCommands` sagt die Sandbox-Doku: „run matching commands outside the sandbox, which means no filesystem restrictions and no network proxy“ und „An excluded command runs with your full access.“ Ein ausgenommenes `gh …` oder `monarch …` dürfte also auch den Haupt-Checkout schreiben. Die weichere Isolation öffnet nur den Trust-Dienst, Dateisystem und Domain-Liste bleiben. Owner-Entscheid vom 26.11.2026, wörtlich: „TLS: enableWeakerNetworkIsolation wie empfohlen, keine excludedCommands.“ Nachtest im Abschnitt „Zweite Runde nach der Aktivierung“.
