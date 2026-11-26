---
id: Q-0201
gate: G-0130
kind: main
rank: A
state: in_review
dod:
  - "Owner-Entscheid vom 25.11.2026 (Chat): Der Haupt-Checkout wird auf OS- und git-Ebene geschützt; der PreToolUse-Hook für Bash ist nur noch Frühwarnung und keine Sicherheitsgrenze"
  - "Schicht 1: .claude/settings.json mit sandbox.enabled, failIfUnavailable true, allowUnsandboxedCommands false, filesystem.denyWrite für HEAD, index, ORIG_HEAD, FETCH_HEAD, refs/heads/develop, refs/heads/main des Haupt-Checkouts, network.allowedDomains nur so viel wie nötig, excludedCommands nur begründet; verweigert der Hook das Schreiben (protect_paths), legt der Owner die Datei an"
  - "Prüfung im Bericht: Session im Haupt-Checkout (Bash dort beschreibbar) und Sandbox der Subagenten (Knight, Warden, Kritiker, auch isolation: worktree)"
  - "Schicht 2: plugin/git-hooks/reference-transaction als reines sh-Skript ohne monarch-Binary; nur im Zustand prepared; CLAUDECODE leer heißt exit 0; sonst Deny bei git-dir == git-common-dir oder Ref refs/heads/develop oder refs/heads/main, Meldung mit Verweis auf ADR-0020 §1; Installation macht der Owner"
  - "Tests in temporären Repos mit verknüpfter Worktree: Commit im Haupt-Checkout verweigert, Commit in der Worktree erlaubt, branch -f develop und branch -D develop (gepackt) aus der Worktree verweigert, fetch erlaubt, ohne CLAUDECODE alles erlaubt"
  - "Schicht 3: cwd-Modell eingefroren (keine Änderung an der cd-Logik); neue feste Sperrliste im Bash-Text: hooksPath, GIT_CONFIG, CLAUDECODE ohne Beachtung der Schreibweise, mit Tests"
  - "Edit, Write, MultiEdit und NotebookEdit: Zielpfad per realpath (Symlinks) gegen den Haupt-Checkout geprüft, mit Tests"
  - "ADR-0020 §1 neu: Bedrohungsmodell, drei Schichten, Restrisiko wörtlich nach dem Owner-Entscheid"
  - "dotnet build -warnaserror ohne Warnungen, dotnet test 0 fehlgeschlagen; bestehende Tests nicht aufgeweicht"
scope:
  include: [".claude/settings.json", "plugin/git-hooks/**", "plugin/README.md", "src/Monarch.Policy/**", "src/Monarch.Cli/HookRunner.cs", "docs/adr/0020-selbst-aufraeumen.md", "tests/**"]
  exclude: []
estimate: { session_pct: 25, weekly_pct: 5, gold_usd: 0 }
boss: "Hook-Tests in temporären Repos (Haupt-Checkout-Commit, branch -f/-D develop gepackt, fetch, ohne CLAUDECODE) und Policy-Tests (Sperrliste, Edit über Symlink in den Haupt-Checkout) erst rot, dann grün; dotnet test grün"
board: { pr: 163 }
---
# Haupt-Checkout auf OS- und git-Ebene schützen (Sandbox, reference-transaction, Hook nur Frühwarnung)
