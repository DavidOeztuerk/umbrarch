---
id: Q-0202
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Owner-Entscheid vom 25.11.2026 (Chat, nach dem Bericht zu Q-0201): Schicht 1 kommt in die User-Settings ~/.claude/settings.json mit absoluten Pfaden, nicht ins Repo; der Owner fügt sie selbst ein"
  - "Hook: mit CLAUDECODE nicht leer immer refs/heads/develop und refs/heads/main verweigern (Schreibweise egal); im Haupt-Checkout (git-dir == git-common-dir) zusätzlich HEAD und die Branch, auf die HEAD des Haupt-Checkouts zeigt; andere neue oder geänderte Refs erlaubt, auch aus dem Haupt-Checkout"
  - "Tests rot zuerst: worktree add -b aus dem Haupt-Checkout erlaubt; checkout und switch im Haupt-Checkout verweigert; commit im Haupt-Checkout verweigert; update-ref --stdin mit zwei Refs (eine erlaubt, refs/heads/develop) wird ganz abgelehnt, beide unverändert; alle bisherigen Fälle weiter grün"
  - "ADR-0020 §1 und plugin/README.md: User-Settings statt Repo-Datei, neue Hook-Regel, Lücken neu bewertet, Doku-Beleg zum Schreibbereich der Sandbox, Belege zur Gate-Öffnung heute und Vorschlag mit Sandbox, Vorschlag zur Session im Haupt-Checkout (nur vorlegen)"
  - "Keine excludedCommands vorsorglich"
  - "dotnet build -warnaserror ohne Warnungen, dotnet test 0 fehlgeschlagen"
scope:
  include: ["plugin/git-hooks/**", "plugin/README.md", "docs/adr/0020-selbst-aufraeumen.md", "tests/**"]
  exclude: []
estimate: { session_pct: 15, weekly_pct: 3, gold_usd: 0 }
boss: "ReferenceTransactionHookTests: worktree add -b aus dem Haupt-Checkout erlaubt, checkout/switch/commit im Haupt-Checkout verweigert, update-ref --stdin mit develop und einer erlaubten Ref ganz abgelehnt; erst rot, dann grün"
board: { pr: 163 }
---
# Sandbox in den User-Settings, Hook-Regel enger (HEAD und Branch des Haupt-Checkouts)
