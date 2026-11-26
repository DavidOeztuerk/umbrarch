---
id: Q-0218
gate: G-0131
kind: main
rank: A
state: proposed
dod:
  - "Erst nach Owner-Antwort zu ADR-0021 Offen 1 (gilt ci.yml unverändert auch für den Schritt im Job plugin?); vorher nichts an ci.yml"
  - "scripts/check-plugin-version.sh <basis>: bei Änderung unter plugin/ muss die Version in plugin.json höher sein als in der Basis (sort -V), und plugin.json, plugin/scripts/monarch, src/Monarch.Cli/McpTools.cs und der Wrapper-Test nennen dieselbe Version; sonst Exit 1 mit Text"
  - "plugin/tests/plugin-version-test.sh vorher rot, danach grün: Änderung ohne höhere Version 1; mit höherer Version 0; ohne Änderung 0; niedrigere Version 1; Stellen ungleich 1; wrapper-test.sh liest die Version aus dem Wrapper statt 0.1.2 fest"
  - "Version 0.1.3 an allen Stellen (Owner-Entscheid 3); Hinweis für den Owner: zuerst scripts/install-local.sh, dann /plugin install"
  - "Verschärft nur; kein bestehender CI-Schritt aufgeweicht"
scope:
  include: ["scripts/check-plugin-version.sh", "plugin/tests/**", ".github/workflows/ci.yml", "plugin/.claude-plugin/plugin.json", "plugin/scripts/monarch", "src/Monarch.Cli/McpTools.cs", "docs/adr/0021-develop-gruen-und-gate-open.md"]
  exclude: []
estimate: { session_pct: 8, weekly_pct: 2, gold_usd: 0 }
boss: "Ein PR, der plugin/ ändert und die Version nicht erhöht, wird vom Prüfskript rot gemeldet; mit 0.1.3 grün"
---
# Versionspflicht für plugin/ und Version 0.1.3
