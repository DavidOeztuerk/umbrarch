# ADR-0022: Lese-Sperre für Secrets auf OS-Ebene (G-0132)

- Status: Entwurf mit Owner-Entscheiden vom 26.11.2026 (unten). Weder vom Owner noch vom Warden freigegeben.
- Datum: 2026-11-26
- Gate: G-0132, Quest Q-0219
- Bezug: #173, danach #66 und #169; ADR-0011 (Regeln), ADR-0020 §1 (Sandbox in den User-Settings, Hook nur Frühwarnung)

## Owner-Vorgaben

Wörtlich aus der Prompt des Owners vom 26.11.2026 (Auszug, Punkte 2, 3 und 5):

> 2. Design zuerst: ADR-0022 für #173. Inhalt: welche Credential-Pfade (aus der heutigen Hook-Prüfung protect_secrets ableiten, Datei:Zeile), sandbox denyRead für Bash, permissions.deny für das Read-Werkzeug, beides in ~/.claude/settings.json. Die Settings ändere ich selbst im Terminal: gib mir einen fertigen, idempotenten Befehl mit Backup. Proben, die belegen, dass Bash und Read gesperrt sind.
> 3. Reihenfolge fest: Sperre aktiv und per Probe belegt, erst dann Lockerung der Hook-Prüfung für #66 und #169. Der Hook bleibt Frühwarnung (Owner-Entscheid 25.11.).
> 5. Warden-Design-Runde schmal über ADR-0022, vor dem ersten Code-Commit. Kritiker nur frisch gestartet und nur für Belege im ADR.

Owner-Entscheide zum ersten Entwurf, wörtlich aus dem Chat vom 26.11.2026 (Auszug):

> F1 neu: .env.example, .env.sample und .env.template müssen lesbar bleiben (eingecheckte Vorlagen, Agents brauchen sie). Da ein Deny keine Ausnahme zulässt, ersetze auf OS-Ebene ~/**/.env.* durch eine Liste konkreter Secret-Namen (.env, .env.local, .env.*.local, .env.development, .env.production, .env.staging; belege, welche Namen in meinen Projekten unter ~/Projects tatsächlich vorkommen: nur Dateinamen per find, nie Inhalte). Restrisiko offen benennen: andere .env.*-Namen schützt nur der Hook wie heute. Probe ergänzen: .env.example ist per Bash und Read lesbar.
> F2: canary.txt bleibt dauerhaft in der Liste.
>
> Ergänzung §4, Gegenprobe Pflicht: Nach der Sperre funktionieren in der Sandbox gh api user --jq .login, git fetch origin und dotnet restore weiterhin. Vorher belegen, welche Datei gh und git für Zugangsdaten nutzen (gh auth status, git config --get credential.helper; nur Pfade und Namen, nie Inhalte) und ob einer der Globs sie trifft.

Owner-Entscheid zu F3, wörtlich aus dem Chat vom 26.11.2026:

> Owner-Entscheid F3: .env.test in die Liste aufgenommen (ich habe den Befehl aus §3 um "~/**/.env.test" ergänzt und so ausgeführt; beide Listen haben jetzt 14 Einträge). .env.vor-umbenennung kommt nicht in die Liste: vermutlich alte Kopie einer echten .env, darum kümmere ich mich selbst.

Owner-Antworten zu den ersten Probe-Ergebnissen, wörtlich aus dem Chat vom 26.11.2026 (Auszug):

> F4 / n, o, p: Neu ausführen, jede Probe als eigener Aufruf, nur absolute Pfade, kein cd. Für die Proben ausdrücklich erlaubt: in sh -c '…' verpacken, damit sie die Sandbox erreichen (nur Proben, keine Arbeit). Dazu neue Probe o2, die eigentliche Gefahr: ln ~/.umbrarch-probe/.env <W>/hl.txt (Quelle außerhalb von W), dann wc -c <W>/hl.txt; ebenso cp und ln -s von dort. Nie Inhalte ausgeben, nur Größe oder Fehler. Ergebnis:
> - sperrt alles (an ln, cp, mv oder wc): Sperre belegt.
> - kommt ein Inhalt-Lesen durch: stoppen, F4 mit Befund vorlegen (denyWrite allein hilft gegen Hardlinks von außen evtl. nicht, das gehört dann in die Frage).
>
> i: .env.sample und .env.template im Hook freigeben ist eine Lockerung und kommt in Q-0220 (nach belegter Sperre), nicht jetzt. i gilt für Sandbox und Permission-Schicht als bestanden; den Hook-Teil in §5 als Q-0220-Punkt aufnehmen.
>
> m: Probe auf dotnet restore mit denselben Flags wie scripts/test-local.sh umstellen und einmal laufen lassen. Kein Vergleichslauf.

## Kurz in einfacher Sprache

Heute schützt nur der Hook die Credential-Dateien. Er rät aus dem Text eines Befehls, ob eine solche Datei gelesen wird. Das führt zu Fehlalarmen (#66, #169), und es lässt Lücken: Ein Befehl, den der Hook nicht als lesend kennt, kommt durch. Neu sperrt das Betriebssystem das Lesen selbst: die Sandbox für alles, was über Bash läuft, und eine Permission-Regel für das Read-Werkzeug. Erst wenn beides per Probe belegt ist, darf der Hook bei Suchmustern und jq-Ausdrücken weniger streng sein. Er bleibt als Frühwarnung bestehen.

## 1. Welche Pfade

Abgeleitet aus der heutigen Prüfung `protect_secrets`:

- Eingebaute Globs, `src/Monarch.Policy/ToolCallTypes.cs:125-126`: `**/.env`, `**/.env.*`, `**/*.key`, `**/*.p8`, `**/*.pfx`, `**/keycloak.env`, `**/.netrc`, `**/id_rsa*`.
- Ausnahme, `ToolCallTypes.cs:103`: `**/.env.example`.
- Zusätzliche Globs des Projekts: keine (`.monarch/system.yaml:26`, `protect_secrets_extra: []`).
- Die Regel selbst: `src/Monarch.Policy/ToolCallPolicy.cs:376-404`. Sie prüft das Read-Werkzeug (:378-381), das Grep-Werkzeug (:382-388) und bei Bash nur die Kommandowörter aus `SecretReaders` (:36-38, :393) sowie Umleitungen mit `<` (:400-402). Der Vergleich läuft über `IsSecret` (:432-436).

Liste für die Sperre (Owner-Entscheide F1 und F3): `**/.env.*` wird durch konkrete Namen ersetzt, alle übrigen Globs bleiben. Jeder Eintrag gilt unter dem Home-Verzeichnis (`~/**/<Name>`):

`.env`, `.env.local`, `.env.*.local`, `.env.development`, `.env.production`, `.env.staging`, `.env.test` (F3), `*.key`, `*.p8`, `*.pfx`, `keycloak.env`, `.netrc`, `id_rsa*`.

Dazu kommt der Prüfeintrag `~/.umbrarch-probe/canary.txt`, dauerhaft (Owner-Entscheid F2). Der Hook kennt ihn nicht; eine Probe auf `canary.txt` zeigt darum, ob die Sperre allein wirkt (Abschnitt 4).

Welche `.env`-Namen unter `~/Projects` vorkommen, nur Dateinamen, ohne `node_modules` und `.git` (26.11.2026, 36 Dateien):

```
find ~/Projects -name '.env*' -not -path '*/node_modules/*' -not -path '*/.git/*' | sed 's#.*/##' | sort | uniq -c
```

| Name | Anzahl | Durch die Liste gesperrt |
|---|---|---|
| `.env.example` | 22 | nein, lesbar |
| `.env` | 9 | ja |
| `.env.vor-umbenennung` | 1 | nein, nur Hook |
| `.env.test` | 1 | ja (F3) |
| `.env.staging.example` | 1 | nein, lesbar |
| `.env.secrets.example` | 1 | nein, lesbar |
| `.env.production.example` | 1 | nein, lesbar |

`.env.sample` und `.env.template` kommen unter `~/Projects` nicht vor; die Liste sperrt sie nicht. `.env.vor-umbenennung` bleibt außerhalb der Liste; darum kümmert sich der Owner selbst (F3).

## 2. Wie gesperrt wird

Beides kommt in `~/.claude/settings.json`, mit derselben Liste:

- **Bash:** `sandbox.filesystem.denyRead`. Das gilt für jeden Prozess, den Bash startet, auch für Skripte, Interpreter und Befehle, die der Hook nicht kennt (z. B. `wc`, `python3 skript.py`).
- **Read-Werkzeug:** `permissions.deny` mit `Read(~/**/<Name>)`. Laut Doku wendet Claude Code diese Regeln nach bestem Bemühen auch auf Grep und Glob an.

Was die Doku dazu sagt (gelesen am 26.11.2026):

- Unter macOS wirken `*`, `?` und `**` in allen vier Listen von `sandbox.filesystem`; unter Linux nur eingeschränkt. Quelle: https://code.claude.com/docs/en/settings-reference, Abschnitt „Where wildcards work in sandbox paths“. Die Sperre gilt damit für den Mac des Owners, nicht für Linux.
- `Read`-Deny-Regeln übernimmt Claude Code auch in `denyRead`. Quelle: dieselbe Seite. Die Sperre steht trotzdem ausdrücklich in beiden Listen, so wie vom Owner vorgegeben.
- Eine Allow-Regel kann keine Ausnahme aus einer Deny-Regel schneiden. Quelle: https://code.claude.com/docs/en/permissions. Darum enthält die Liste keinen Glob, der Vorlagen wie `.env.example` trifft.

Zugangsdaten von gh, git und dotnet (nur Namen und Pfade, 26.11.2026):

- `gh auth status`: angemeldet bei github.com, Ablage `(keyring)`, Protokoll https. Im Ordner `~/.config/gh/` liegen `config.yml` und `hosts.yml`.
- `git config --show-origin --get-all credential.helper`: `osxkeychain`, gesetzt in `/Applications/Xcode.app/Contents/Developer/usr/share/git-core/gitconfig`. `origin` ist https (`git remote -v`).
- `dotnet nuget locals all --list`: `~/.local/share/NuGet/http-cache`, `~/.nuget/packages/`, temp unter `/tmp/claude-501/NuGetScratch`.

Kein Glob der Liste trifft `config.yml`, `hosts.yml`, den Schlüsselbund oder die NuGet-Ordner. Ob die Befehle danach laufen, zeigen die Proben k bis m.

## 3. Befehl für den Owner

Im eigenen Terminal ausführen. Der Befehl legt zuerst ein Backup an, ergänzt nur fehlende Einträge (bestehende Reihenfolge bleibt) und prüft mit `jq -e`, dass alle 14 Einträge in beiden Listen stehen. Erst dann überschreibt er die Datei, per `cat >`, damit Rechte und Inode bleiben. Bei einem Fehler bricht er vorher ab, und die Temp-Datei wird gelöscht. Ein zweiter Lauf lässt den Inhalt gleich, legt aber wieder ein Backup an.

```bash
(
  set -euo pipefail
  S="$HOME/.claude/settings.json"
  P='["~/**/.env", "~/**/.env.local", "~/**/.env.*.local", "~/**/.env.development",
      "~/**/.env.production", "~/**/.env.staging", "~/**/.env.test",
      "~/**/*.key", "~/**/*.p8", "~/**/*.pfx",
      "~/**/keycloak.env", "~/**/.netrc", "~/**/id_rsa*",
      "~/.umbrarch-probe/canary.txt"]'
  B="$S.bak-$(date +%Y%m%d-%H%M%S)"
  cp -p "$S" "$B" && echo "Backup: $B"
  T="$(mktemp "$S.XXXXXX")"; trap 'rm -f "$T"' EXIT
  jq --argjson p "$P" '
    def addu($n): reduce $n[] as $e (.; if any(.[]; . == $e) then . else . + [$e] end);
    .sandbox.filesystem.denyRead = ((.sandbox.filesystem.denyRead // []) | addu($p))
    | .permissions.deny = ((.permissions.deny // []) | addu($p | map("Read(" + . + ")")))
  ' "$S" > "$T"
  jq -e --argjson p "$P" '
    .sandbox.enabled == true
    and .sandbox.allowUnsandboxedCommands == false
    and (($p - .sandbox.filesystem.denyRead) | length == 0)
    and ((($p | map("Read(" + . + ")")) - .permissions.deny) | length == 0)
  ' "$T" > /dev/null
  cat "$T" > "$S"
  jq '{denyRead: .sandbox.filesystem.denyRead, readDeny: [.permissions.deny[] | select(startswith("Read("))]}' "$S"
)
```

Probedateien anlegen (nur Platzhalter, kein echtes Secret). Der zweite Teil legt Dateien in einem Ordner an, in den die Sandbox schreiben darf (in der Gate-Worktree, nicht committet, nach den Proben von der Session gelöscht):

```bash
( mkdir -p ~/.umbrarch-probe/a/b/c ~/.umbrarch-probe/ci && cd ~/.umbrarch-probe && \
  for f in .env .env.local .env.prod.local .env.development .env.production .env.staging .env.test \
           a.key ci/A.KEY a.p8 a.pfx keycloak.env .netrc id_rsa_probe canary.txt a/b/c/a.key \
           .env.example .env.sample .env.template ok.txt; do printf 'PROBE=1\n' > "$f"; done && \
  printf 'PROBE=1\n' > ~/id_rsa_umbrarch_probe )
( W=~/Projects/umbrarch/.claude/worktrees/G-0132/.umbrarch-probe-w && mkdir -p "$W" && cd "$W" && \
  for f in .env .env.local .env.production; do printf 'PROBE=1\n' > "$f"; done )
```

Danach eine neue Session starten, damit die Settings gelten.

Zurück: `cp -p <Backup> ~/.claude/settings.json`.

## 4. Proben

Die Session führt sie nach der Aktivierung aus und trägt die Ergebnisse hier ein. Bash-Proben nutzen `wc -c`: Der Hook kennt `wc` nicht als Leser (`ToolCallPolicy.cs:36-38`), also zeigt ein Fehler die OS-Sperre. Ausgegeben wird nur die Größe, nie der Inhalt. Pfade ohne Ordner liegen in `~/.umbrarch-probe`, `W` ist `~/Projects/umbrarch/.claude/worktrees/G-0132/.umbrarch-probe-w`.

| Probe | Befehl / Werkzeug | Erwartet | Belegt |
|---|---|---|---|
| a | `wc -c` auf `.env .env.local .env.prod.local .env.development .env.production .env.staging .env.test a.key a.p8 a.pfx keycloak.env .netrc id_rsa_probe` | je „Operation not permitted“ | Sandbox sperrt alle dreizehn Globs für Bash |
| a2 | `wc -c ~/id_rsa_umbrarch_probe a/b/c/a.key` | je „Operation not permitted“ | `~/**/` gilt direkt unter `~` und tief verschachtelt |
| a3 | `ls ci` zeigt `A.KEY` in Großbuchstaben, dann `wc -c ci/A.KEY` | „Operation not permitted“, sonst Restrisiko | Groß- und Kleinschreibung (eigener Ordner, weil das Dateisystem `a.key` und `A.KEY` nicht unterscheidet) |
| n | `sh -c 'mv W/.env W/moved.txt; wc -c W/moved.txt'`, absolute Pfade, kein `cd` | Fehler bei `mv` oder `wc` | Umbenennen umgeht die Sperre nicht |
| o | `sh -c 'ln W/.env.local W/linked.txt; wc -c W/linked.txt'` | Fehler bei `ln` oder `wc` | Hardlink umgeht die Sperre nicht |
| o2 | `sh -c` je für `ln`, `cp` und `ln -s` von `~/.umbrarch-probe/.env` nach `W/hl.txt`, `W/cp.txt`, `W/sl.txt`, dann `wc -c` auf das Ziel | Fehler bei `ln`, `cp` oder `wc` | Kopie oder Link von einer Quelle außerhalb von `W` umgeht die Sperre nicht (Owner) |
| p | `sh -c 'ln -s W/.env.production W/sym.txt; wc -c W/sym.txt'` | Fehler bei `wc` | Symlink umgeht die Sperre nicht |
| b | `wc -c ok.txt` | `8` | Gegenprobe, nicht alles gesperrt |
| c | `wc -c canary.txt` | „Operation not permitted“ | Sperre ohne Hook |
| d | Read `canary.txt` | Deny von Claude Code, nicht vom Hook | Read-Regel ohne Hook |
| e | Read `.env` | Deny; welcher Text, wird notiert | Doppelte Sperre |
| f | Read `ok.txt` | Inhalt `PROBE=1` | Gegenprobe Read |
| g | Grep-Werkzeug, Pfad `.env.production` | Deny (laut Doku nur nach bestem Bemühen) | Grep, nur zur Kenntnis |
| h | `wc -c .env.example .env.sample .env.template` | je `8` | Vorlagen per Bash lesbar (F1) |
| i | Read `.env.example`, `.env.sample`, `.env.template` | je Inhalt `PROBE=1` | Vorlagen per Read lesbar (F1) |
| j | `scripts/test-local.sh` | wie vor der Aktivierung | Kein Nebenschaden an Build und Tests |
| k | `gh api user --jq .login` | `DavidOeztuerk` | gh-Anmeldung geht weiter |
| l | `git fetch origin` | Exit 0 | git über https geht weiter |
| m | `dotnet restore --disable-build-servers -m:1 -nodeReuse:false` (Flags wie `scripts/test-local.sh`, Owner) | Exit 0 | NuGet geht weiter |

Grenze der Proben: Für die Globs der Read-Regel gibt es keine Probe ohne Hook, weil der Hook jeden Namen der Liste ebenfalls sperrt. Probe d belegt die Read-Regel nur für den festen Pfad `canary.txt`. Probe e zeigt, welche Schicht zuerst antwortet. Die Globs selbst belegt Probe a für `denyRead`.

Bestanden ist die Sperre, wenn a, a2, c, d, n, o, o2 und p sperren und b, f, h, i, j, k, l und m laufen. Erst dann beginnt Q-0220 (Lockerung für #66 und #169). Sperren n oder o nicht, stoppt die Session und legt dem Owner die Frage F4 vor; die Sperre gilt dann nicht als belegt. Nach den Proben löscht die Session `W`. `W` ist nicht von git ignoriert. Bis `W` gelöscht ist, gibt es darum kein `git add -A` und kein `git add .`, nur `git add` mit genannten Pfaden.

Ergebnisse vom 26.11.2026 (Session nach der Aktivierung; Settings geprüft mit `jq` auf `~/.claude/settings.json`: je 14 Einträge in `denyRead` und als `Read(...)` in `permissions.deny`, `sandbox.enabled` true, `allowUnsandboxedCommands` false, keine `excludedCommands`):

| Probe | Ergebnis | Bestanden |
|---|---|---|
| a | alle 13 Dateien: `wc: <Datei>: open: Operation not permitted` | ja |
| a2 | beide: `open: Operation not permitted` | ja |
| a3 | `ls ci` zeigt `A.KEY`; `wc -c ci/A.KEY`: `open: Operation not permitted` | ja, Groß- und Kleinschreibung gesperrt |
| n | Der Befehl lief nicht. Claude Code: `mv from '…/worktrees/G-0132/.env' was blocked by a deny rule.` Die Sperre kam von der Permission-Schicht, nicht von der Sandbox, und mit falsch aufgelöstem Pfad (das `cd` nach `W` wurde nicht beachtet). Ob die OS-Sperre ein Umbenennen abfängt, ist nicht belegt. | offen |
| o, p | nicht gelaufen (im selben Aufruf wie n; nach Regel 2 keine Variante) | offen |
| b | `8 ok.txt` | ja |
| c | `open: Operation not permitted` | ja |
| d | Read: `File is in a directory that is denied by your permission settings.` (Claude Code, nicht der Hook) | ja |
| e | Read `.env`: derselbe Text wie d, die Permission-Schicht antwortet vor dem Hook | ja |
| f | Inhalt `PROBE=1` | ja |
| g | nicht ausführbar: Das Grep-Werkzeug stand in dieser Session nicht zur Verfügung | offen, nur zur Kenntnis |
| h | je `8` für `.env.example`, `.env.sample`, `.env.template` | ja |
| i | `.env.example`: `PROBE=1`. `.env.sample` und `.env.template`: Hook-Deny `PreToolUse:Read hook error: Credential-Dateien liest Monarch nie. …` (Hook-Glob `**/.env.*`, Ausnahme nur `**/.env.example`, `ToolCallTypes.cs:103`) | ja für Sandbox und Permission-Schicht (Owner); der Hook-Teil ist ein Q-0220-Punkt (§5) |
| j | Build grün; Domain 97/0 Fehlschläge, Policy 1474/0, Infrastructure 193/0 (1 übersprungen), Cli 789/1. Der eine Fehlschlag ist `Real_ps_finds_the_hull_of_a_real_child_and_the_hook_leaves_it_running` (`/bin/ps`: Operation not permitted), die in `scripts/test-local.sh:10-13` genannte bekannte Grenze. | ja, wie vor der Aktivierung |
| k | `DavidOeztuerk`, Exit 0 | ja |
| l | Exit 0 | ja |
| m | `dotnet restore` (ohne Flags) hing zweimal und endete mit Exit 1 und „0 Fehler“, beim zweiten Lauf nach 15:02 min. Das Muster passt zu `scripts/test-local.sh:5-6` (ohne `--disable-build-servers` hängt `dotnet build`). Restore samt Build über `test-local.sh` (j) lief. Einen Vergleichslauf von `dotnet restore` vor der Aktivierung gibt es nicht. | nein, wie gefordert; Ursache vermutlich Sandbox-Socket, nicht `denyRead` |

Die Zeilen n, o, p und m oben sind die ersten Läufe. Die Wiederholung nach den Owner-Antworten steht in der nächsten Tabelle.

Stand nach dem ersten Lauf: n, o, p und m offen.

Wiederholung vom 26.11.2026 nach den Owner-Antworten: jede Probe ein eigener Aufruf in `sh -c '…'`, nur absolute Pfade, kein `cd`. Ausgegeben wurden nur Exitcodes, Größen und Fehler.

| Probe | Ergebnis | Bestanden |
|---|---|---|
| n | `mv: rename …/W/.env to …/W/moved.txt: Operation not permitted`, Exit 1; `wc`: No such file or directory | ja, die Sandbox sperrt `mv` |
| o | `ln: …/W/.env.local: Operation not permitted`, Exit 1; `wc`: No such file or directory | ja, die Sandbox sperrt `ln` |
| o2, `ln` | `ln: ~/.umbrarch-probe/.env: Operation not permitted`, Exit 1; `wc`: No such file or directory | ja, die Sandbox sperrt `ln` |
| o2, `cp` | Der Befehl lief nicht. Monarch-Hook: `PreToolUse:Bash hook error: Das Kommando liest eine Credential-Datei. …` Nach Regel 2 keine Variante. | gesperrt vom Hook; die Sandbox-Schicht für `cp` ist nicht belegt |
| o2, `ln -s` | `ln -s` Exit 0; `wc -c …/W/sl.txt: open: Operation not permitted` | ja, gesperrt an `wc` |
| p | `ln -s` Exit 0; `wc -c …/W/sym.txt: open: Operation not permitted` | ja, gesperrt an `wc` |
| m | `dotnet restore --disable-build-servers -m:1 -nodeReuse:false`, mit `DOTNET_CLI_TELEMETRY_OPTOUT=1` wie in `scripts/test-local.sh`: Exit 0 nach 2 s | ja |

Bewertung (Zusammenfassung, kein Owner-Zitat): Kein Inhalt kam durch; jede Probe scheiterte an `ln`, `cp`, `mv` oder `wc`. Nach dem Kriterium des Owners ist die Sperre damit belegt. Einschränkung: `cp` hat der Hook abgefangen, nicht die Sandbox. Ein `cp` muss die Quelle `~/.umbrarch-probe/.env` öffnen, und dieses Öffnen sperrt die Sandbox nachweislich für `wc` (Probe a). Dass sie es auch für `cp` sperrt, ist ein Schluss daraus und nicht eigens geprobt.

Aufräumen: `rm -r W` entfernte die Symlinks `sl.txt` und `sym.txt`. Bei `.env`, `.env.local` und `.env.production` meldete es `Operation not permitted`, bei `W` selbst `Directory not empty`. Die Sandbox sperrt also auch das Löschen von Dateien aus der Liste. `W` löscht der Owner im eigenen Terminal; bis dahin gilt weiter nur `git add` mit genannten Pfaden.

## 5. Danach: Lockerung des Hooks (Q-0220, nur Skizze für die Warden-Runde)

- Grundsatz: Ausgenommen wird genau ein Token, das Muster bzw. der Filter. Kommt im Segment eine Option vor, die nicht auf einer festen Liste steht (auch zusammengezogene Kurzoptionen wie `-ef`, und Optionen mit Wert wie `rg -g`/`--glob`/`--iglob`), gilt die heutige strenge Prüfung aller Tokens.
- #66, `grep` und `rg`: Ohne `-e`/`--regexp` und ohne `-f`/`--file` ist das erste Argument ohne Option das Muster und kein Pfad. Mit `-e`/`--regexp` ist nur dessen Wert das Muster, und jedes Argument ohne Option ist eine Datei. Werte von `-f`/`--file` sind Dateien. Alle Dateien werden weiter geprüft.
- #169, `jq`: Ohne `-f`/`--from-file` ist das erste Argument ohne Option der Filter und kein Pfad. Mit `-f`/`--from-file` gibt es keinen ausgenommenen Filter. Werte von `--rawfile` und `--slurpfile` sind Dateien; Werte von `--arg` und `--argjson` werden weiter geprüft. Vermutung zur Ursache, noch nicht per Test belegt: Nicht `~/.claude/plugins/*.json` trifft, sondern `.key` im Ausdruck `select(.key|test(...))` trifft den Glob `**/*.key`. Der rote Test in Q-0220 klärt das. Eine Ausnahme für `~/.claude/plugins` ist nicht geplant.
- Wächter (bleiben Deny): `grep foo .env`, `grep -f .env x`, `grep foo x .env`, `grep -e x .env`, `grep --regexp=x .env`, `grep --file=.env x`, `grep -ef .env x`, `rg -f .env x`, `rg -g .env x`, `jq . .env`, `jq -f p.jq .env`, `jq --from-file=.env`, `jq --rawfile a .env -n 1`, `jq --slurpfile a .env -n 1`, `cat ~/.netrc`.
- Die Globs des Hooks bleiben unverändert, auch `**/.env.*`. Alle anderen Leser und Regeln bleiben unverändert.
- Vorlagen (Owner-Antwort zu i): Die Ausnahmen des Hooks (`SecretExceptions`, `src/Monarch.Policy/ToolCallTypes.cs:103`, heute nur `**/.env.example`) bekommen `**/.env.sample` und `**/.env.template` dazu. Das ist eine Lockerung und gehört zu Q-0220, also erst nach belegter Sperre.

## Restrisiko

- Andere `.env.*`-Namen (etwa `.env.vor-umbenennung`) schützt nur der Hook wie heute (Owner-Entscheide F1 und F3).
- Nur unter `~`. Dateien außerhalb (etwa in `/private/tmp`) sperrt die Sandbox nicht; dort bleibt nur der Hook.
- Nur macOS. Unter Linux wirken diese Globs in `denyRead` nicht (Doku, Abschnitt 2).
- Werkzeuge außerhalb der Sandbox: MCP-Server und Hooks laufen nicht in ihr. Das Grep-Werkzeug ist laut Doku nur nach bestem Bemühen abgedeckt, etwa nicht bei einer Suche über ein ganzes Verzeichnis.
- Weitere Credential-Orte (etwa `~/.aws`) stehen nicht in der Liste, weil der Hook sie heute nicht kennt. Eine Erweiterung entscheidet der Owner.
- Die Sperre hält nur, solange `sandbox.allowUnsandboxedCommands` auf `false` steht und es keine `excludedCommands` gibt (heute so; der Befehl in §3 prüft das Erste).
- Schon heute im Hook: Tokens mit `-` am Anfang prüft er nicht (`IsSecretToken`, `ToolCallPolicy.cs:429-430`). `grep -r --include=.env.test PASS .` liest also eine Datei, die weder Hook noch Sandbox schützt. Für Namen der Liste greift die Sandbox.
- Groß- und Kleinschreibung: Der Hook vergleicht ohne Rücksicht darauf (`MatchesAnyCI`, `ToolCallPolicy.cs:432-436`). Ob die Sandbox `A.KEY` sperrt, zeigt Probe a3; wenn nicht, gilt das als Restrisiko.

## Offene Fragen an den Owner

- **F4, nur falls Probe n oder o nicht sperrt:** `denyRead` gilt für den Pfadnamen. Wird eine Datei umbenannt oder hart verlinkt, kann der neue Name lesbar sein. Vorschlag dann: dieselbe Liste auch in `sandbox.filesystem.denyWrite`. Folge: Bash kann solche Dateien dann nicht mehr anlegen, ändern, umbenennen oder löschen. Ob das `mv` und `ln` sperrt, zeigt eine zweite Probe.

F3 ist entschieden (oben, Owner-Entscheide).
