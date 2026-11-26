---
id: Q-0218
gate: G-0131
kind: main
rank: A
state: in_progress
dod:
  - "scripts/check-plugin-version.sh <basis>: bei Änderung unter plugin/ muss die Version in plugin.json höher sein als in der Basis (sort -V), und plugin.json, plugin/scripts/monarch und src/Monarch.Cli/McpTools.cs nennen dieselbe Version; sonst Exit 1 mit Text"
  - "plugin/tests/plugin-version-test.sh vorher rot, danach grün: Änderung ohne höhere Version 1; mit höherer Version 0; ohne Änderung 0; niedrigere Version 1; Stellen ungleich 1"
  - "ci.yml Job plugin: neuer Schritt nur bei pull_request mit origin/<base_ref> als Basis (fetch-depth 0) und Aufruf des neuen Tests (Owner-Antwort 1); macOS und Nachtlauf unverändert"
  - "Version 0.1.3 an allen Stellen (Owner-Entscheid 3); feste Versionen in wrapper-test.sh:107/:113, VisibleLayerNameTests.cs:45, McpTests.cs:40 lesen die Version aus Wrapper oder plugin.json; Hinweis für den Owner: zuerst scripts/install-local.sh, dann /plugin install"
  - "Verschärft nur; kein bestehender CI-Schritt aufgeweicht"
scope:
  include: ["scripts/check-plugin-version.sh", "plugin/tests/**", ".github/workflows/ci.yml", "plugin/.claude-plugin/plugin.json", "plugin/scripts/monarch", "src/Monarch.Cli/McpTools.cs", "tests/Monarch.Cli.Tests/VisibleLayerNameTests.cs", "tests/Monarch.Cli.Tests/McpTests.cs", "docs/adr/0021-develop-gruen-und-gate-open.md"]
  exclude: []
estimate: { session_pct: 8, weekly_pct: 2, gold_usd: 0 }
boss: "Ein PR, der plugin/ ändert und die Version nicht erhöht, wird vom Prüfskript rot gemeldet; mit 0.1.3 grün"
---
# Versionspflicht für plugin/ und Version 0.1.3
