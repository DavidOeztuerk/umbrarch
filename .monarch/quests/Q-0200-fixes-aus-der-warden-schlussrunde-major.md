---
id: Q-0200
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Owner-Entscheide vom 25.11.2026 (Chat) zu den drei major der Schlussrunde (#issuecomment-7161665432); je Fall Test rot zuerst, Namen im PR"
  - "Grundsatz des Owners: unklar heißt nicht erlaubt, ohne jede Schreibweise einzeln nachzubauen"
  - "cd/pushd mit einer Option außer -L/-P (auch zsh -q, -s): das cwd-Modell behält das alte Verzeichnis und das Ziel (Vereinigung); Tests cd -q <Haupt-Checkout> und cd -s <Haupt-Checkout> && git commit aus einer Worktree: Deny"
  - "Ziel existiert nicht: das cwd-Modell behält das alte Verzeichnis und das Ziel (Vereinigung), ohne Lexer für &&; Test cd /tmp/<fehlt>; git commit im Haupt-Checkout: Deny; mkdir X && cd X && git commit in einer Worktree bleibt erlaubt"
  - "cd mit zwei Operanden, pushd +N/-N und popd +N/-N: protect_main_checkout verweigert jeden schreibenden git-Befehl, der in diesem Befehl danach kommt, mit dem Grund „Arbeitsverzeichnis nicht eindeutig (cd/pushd-Variante); nutze git -C <Gate-Worktree>.“; Tests für cd OLD NEW, pushd +1, pushd -1, popd +1 mit anschließendem git commit"
  - "Dieselbe Vereinigung in DirectoryTargets und ProjectTargets, soweit sie für den Haupt-Checkout-Schutz zählt"
  - "gate cleanup: git diff --cached --name-only --no-renames mit Prüfung des Exit-Codes; Commit mit Pfadangabe (git commit -m … -- .monarch); GateCleanedUp erst nach der Prüfung; Test git mv einer Datei außerhalb .monarch nach .monarch/: Exit 1, alter Pfad im Text, nichts gepusht, Worktree bleibt"
  - "ADR-0020 §1 nennt die Regel als Satz; Offen 1 bis 3 und §3 mit den Owner-Entscheiden wörtlich"
  - "dotnet build -warnaserror ohne Warnungen, dotnet test 0 fehlgeschlagen; bestehende Tests nicht aufgeweicht"
scope:
  include: ["src/Monarch.Policy/BashAnalysis.cs", "src/Monarch.Policy/ToolCallPolicy.cs", "src/Monarch.Policy/ProtectMainCheckoutRule.cs", "src/Monarch.Policy/ProjectTargets.cs", "src/Monarch.Cli/GateCleanupCommands.cs", "docs/adr/0020-selbst-aufraeumen.md", "tests/**"]
  exclude: []
estimate: { session_pct: 12, weekly_pct: 2, gold_usd: 0 }
boss: "Neue Tests für jeden Fall aus der Schlussrunde (cd -q, cd -s, cd OLD NEW, pushd OLD NEW, cd zu fehlendem Ziel, pushd +1, cd +1, popd +1, git mv nach .monarch) erst rot, dann grün; dotnet test grün"
board: { pr: 163 }
---
# Fixes aus der Warden-Schlussrunde (Major 1 bis 3)
