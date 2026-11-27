---
id: Q-0219
gate: G-0132
kind: main
rank: A
state: in_review
dod:
  - "ADR-0022 leitet die Credential-Pfade aus protect_secrets ab (ToolCallTypes.cs:125-126, ToolCallPolicy.cs:375-436)"
  - "Fertiger, idempotenter Settings-Befehl mit Backup für den Owner"
  - "Proben für Bash und Read, Ergebnisse nach der Aktivierung im ADR"
  - "Owner legt vor; schmale Warden-Design-Runde mit approve vor dem ersten Code-Commit"
scope:
  include: ["docs/adr/0022-*.md"]
  exclude: []
estimate: { session_pct: 12, weekly_pct: 2, gold_usd: 0 }
boss: "Proben nach der Aktivierung: Bash und Read auf die Probe-Datei werden vom OS bzw. der Permission-Regel verweigert"
board: { pr: 178 }
---
# Design: ADR-0022 Lese-Sperre für Secrets (sandbox denyRead, permissions.deny), Proben (#173)
