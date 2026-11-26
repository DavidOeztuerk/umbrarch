---
id: Q-0197
gate: G-0130
kind: main
rank: A
state: cleared
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen, Q-0193 und Q-0196 fertig"
  - "gate merge setzt den Gate-Status in der Gate-Datei auf merged; merged zählt zu den geschlossenen Gate-Zuständen (GateMergeCommands.cs:20), ein zweites gate merge und ein spätes gate verdict weisen das Gate ab, gate check bleibt möglich (Test)"
  - "gate cleanup verweigert ohne GateMerged und ohne IntegrationRunChecked green für das Gate, so kommt nie ein Skip-Marker vor dem Merge auf den Branch"
  - "Ablauf laut ADR-0020 Abschnitt 6, alles per git -C: Monarch-Nachtrag in der Gate-Worktree committen (Skip-Marker in der ersten Zeile), Gate-Branch mit Refspec pushen, git worktree remove ohne --force; verweigert git das Entfernen: Grund und Dateien nennen, kein Text schlägt --force vor; Fehlerpfade und Wiederholung laut ADR"
  - "gate carry laut ADR-0020 Abschnitt 4: übernimmt nur das Feld state gemergter Gates und ihrer Quests in die Gate-Worktree, keine Ledger-Dateien, kein direkter Push auf develop; wiederholt ohne Wirkung"
  - "GateOnlyMain verweigert gate cleanup, gate carry und gate move-quest in Subagenten (Policy-Test)"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["src/Monarch.Cli/**", "src/Monarch.Policy/ProtectBranchesRule.cs", "tests/**"]
  exclude: []
estimate: { session_pct: 16, weekly_pct: 4, gold_usd: 0 }
boss: "Integrationstest mit Temp-Repo und Bare-Remote: cleanup vor GateMerged deny; danach Commit mit Skip-Marker auf dem Gate-Branch im Remote, Worktree entfernt, Gate-Datei merged; carry übernimmt nur state; Subagent-Aufruf deny"
board: { pr: 163 }
---
# gate cleanup <G> und Gate-Status merged; Monarch-Stand gemergter Gates weitertragen (#44, #36, #46, #139)
