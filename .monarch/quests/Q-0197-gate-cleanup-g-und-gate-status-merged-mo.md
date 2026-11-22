---
id: Q-0197
gate: G-0130
kind: main
rank: A
state: proposed
dod:
  - "Voraussetzung: Designrunde von Q-0192 abgeschlossen, Q-0193 fertig (ProtectBranchesRule.cs; worktree remove muss erlaubt bleiben) und Q-0196 fertig (GateCommands.cs)"
  - "gate merge setzt den Gate-Status in der Gate-Datei auf merged"
  - "gate cleanup <G> verweigert ohne GateMerged für G im Ledger (kein [skip ci] vor dem Merge)"
  - "Danach alles per git -C: Commit des Monarch-Nachtrags in der Gate-Worktree mit [skip ci], Push des Gate-Branchs mit Refspec, git worktree remove ohne --force; schmutzige Worktree: Abbruch mit Hinweis, nie --force"
  - "Ein Befehl laut ADR-0020 bringt den Monarch-Stand gemergter Gates in die nächste Gate-Worktree; develop bekommt ihn mit dem nächsten Merge, kein direkter Push auf develop"
  - "GateOnlyMain (ProtectBranchesRule.cs:41-56) verweigert gate cleanup in Subagenten (Policy-Test)"
  - "Warden: höchstens eine Delta-Runde, danach ggf. schmale Prüfung nur des Fixes (Standardregel 3)"
scope:
  include: ["src/Monarch.Cli/**", "src/Monarch.Policy/ProtectBranchesRule.cs", "tests/**"]
  exclude: []
estimate: { session_pct: 16, weekly_pct: 4, gold_usd: 0 }
boss: "Integrationstest mit Temp-Repo und Bare-Remote: cleanup vor GateMerged deny; danach Commit mit [skip ci] auf dem Gate-Branch im Remote, Worktree entfernt, Gate-Datei merged; Subagent-Aufruf deny"
---
# gate cleanup <G> und Gate-Status merged; Monarch-Stand gemergter Gates weitertragen (#44, #36, #46, #139)
