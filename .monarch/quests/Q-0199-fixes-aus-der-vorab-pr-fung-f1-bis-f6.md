---
id: Q-0199
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Spezifikation: Fix-Auftrag des Owners vom 25.11.2026 (monarch-proof/grok/fix-G0130-review.md), Abschnitte 1 bis 4 wörtlich; je Befund Test rot zuerst, Namen im PR"
  - "F1 (major, Schutzlücke): cd/pushd mit einer Option, die bash und zsh verschieden lesen (pushd -n, cd -e), hält im cwd-Modell altes und neues Verzeichnis; pushd -n /tmp; git commit im Haupt-Checkout wird verweigert"
  - "F2 (major): gate_plan mit zwei Quests aus demselben alten Gate entfernt beide aus dessen Liste (ein Schreibvorgang je Gate), je ein QuestMoved, ledger verify 0"
  - "F3 (major): gate cleanup verweigert, wenn Dateien außerhalb von .monarch gestaged sind; nichts committet, gepusht oder entfernt"
  - "F4 (major): gate carry und gate move-quest verweigern im Haupt-Checkout (.git ist Verzeichnis); GateCarryTests laufen in einer Worktree"
  - "F5 (minor): move-quest ohne Gate-Wechsel schreibt kein QuestMoved; quest accept meldet ResetPending nur mit dem einheitlichen Satz"
  - "F6 (minor): Texte zur Weckzeit sagen + 1 min statt + 3 min (MonarchApp.cs, WakeCommands.cs, plugin/README.md); Grace und ADR-0008 unverändert"
  - "dotnet build -warnaserror ohne Warnungen, dotnet test 0 fehlgeschlagen; andere bestehende Tests nur dort geändert, wo der Auftrag es erlaubt"
scope:
  include: ["src/Monarch.Policy/BashAnalysis.cs", "src/Monarch.Policy/ToolCallPolicy.cs", "src/Monarch.Cli/McpTools.cs", "src/Monarch.Cli/GateCleanupCommands.cs", "src/Monarch.Cli/GateCarryCommands.cs", "src/Monarch.Cli/GateCommands.cs", "src/Monarch.Cli/QuestCommands.cs", "src/Monarch.Cli/MonarchApp.cs", "src/Monarch.Cli/WakeCommands.cs", "plugin/README.md", "tests/**"]
  exclude: []
estimate: { session_pct: 10, weekly_pct: 2, gold_usd: 0 }
boss: "Die neuen Tests aus dem Auftrag (Dir_options_read_differently_by_bash_and_zsh_keep_the_main_checkout, Gate_plan_with_two_quests_from_one_gate_removes_both, Staged_files_outside_monarch_stop_the_cleanup, Refuses_in_the_main_checkout je carry und move-quest, Repairing_only_the_target_list_logs_no_QuestMoved) erst rot, dann grün; dotnet test grün"
board: { pr: 163 }
---
# Fixes aus der Vorab-Prüfung (F1 bis F6)
