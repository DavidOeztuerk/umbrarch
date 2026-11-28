---
id: Q-0228
gate: G-0134
kind: main
rank: A
state: in_review
dod:
  - "plugin.json, MONARCH_VERSION im Wrapper, ServerVersion in McpTools.cs auf 0.2.0"
  - "Versionstests auf 0.2.0 angepasst, keine Prüfung entfernt"
  - "wrapper-test.sh und plugin-version-test.sh grün"
scope:
  include: ["plugin/.claude-plugin/plugin.json", "plugin/scripts/monarch", "plugin/tests/wrapper-test.sh", "src/Monarch.Cli/McpTools.cs", "tests/Monarch.Cli.Tests/McpTests.cs", "tests/Monarch.Cli.Tests/VisibleLayerNameTests.cs"]
  exclude: []
estimate: { session_pct: 3, weekly_pct: 1, gold_usd: 0 }
boss: "Alle vier Versionsstellen zeigen 0.2.0; scripts/check-plugin-version.sh akzeptiert den PR"
---
# Plugin-Version 0.2.0 (ADR-0021 §3): plugin.json, Wrapper, McpTools und Versionstests
