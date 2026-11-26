namespace Monarch.Policy;

/// <summary>Regel <c>protect_main_checkout</c> (ADR-0020 Abschnitt 1): keine schreibenden Git-Befehle im Haupt-Checkout eines erweckten Projekts.</summary>
public static partial class ToolCallPolicy
{
    private static readonly string[] MainCheckoutWriters = ["checkout", "switch", "reset", "merge", "pull", "commit", "rebase", "stash"];

    private const string MainCheckoutReason =
        "Der Haupt-Checkout bleibt unverändert (Standardregel 2). Arbeite in der Gate-Worktree. Eine neue legt der Owner im eigenen Terminal an "
        + "(`git -C <Haupt-Checkout> worktree add --no-track .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop`) und startet die Session dort.";

    /// <summary>Feste Sperrliste (ADR-0020 §1, Schicht 3): Wörter, mit denen sich der git-Hook umgehen ließe. Teilstring, ohne Beachtung der Schreibweise.</summary>
    private static readonly string[] BannedWords = ["hooksPath", "GIT_CONFIG", "CLAUDECODE"];

    private const string BannedWordsReason =
        "Der Bash-Befehl nennt hooksPath, GIT_CONFIG oder CLAUDECODE; damit ließe sich der git-Hook umgehen (ADR-0020 §1, Schicht 3). "
        + "Suchen mit dem Grep-Werkzeug, Texte über eine Datei (git commit -F <datei>, gh … --body-file <datei>).";

    private const string FileToolReason = "Der Haupt-Checkout bleibt unverändert (Standardregel 2). Ändere die Datei in der Gate-Worktree.";

    /// <summary>Zielpfad per realpath auflösen (fehlende Komponenten bleiben lexikalisch), dann vom Zielverzeichnis aufwärts das nächste <c>.git</c> suchen.</summary>
    private static bool FileToolTargetsMainCheckout(ToolCall call, PolicyContext ctx)
    {
        if (string.IsNullOrEmpty(call.FilePath)) return false;
        var rs = new PathResolver(ctx.ProjectRoot, call.Cwd);
        var abs = rs.Resolve(PathResolver.ExpandTilde(call.FilePath), rs.Cwd);
        var dir = Path.GetDirectoryName(abs);
        return dir is not null && IsMainCheckoutGitDir(BranchGuard.FindGitDir(dir));
    }

    /// <summary>Option B (ADR-0020 §1, Q-0209): Deny, wenn die Startwurzel der Session ein Haupt-Checkout ist und das Werkzeug schreiben kann.</summary>
    /// Lesen (Read, Grep, Glob …) bleibt erlaubt. Die Wurzel wird per realpath aufgelöst; ein Unterordner des Haupt-Checkouts zählt mit.
    public static PolicyDecision? MainCheckoutSession(string? toolName, string? sessionRoot)
    {
        if (toolName is not ("Bash" or "Edit" or "Write" or "MultiEdit" or "NotebookEdit")) return null;
        if (string.IsNullOrEmpty(sessionRoot) || !Path.IsPathRooted(sessionRoot)) return null;
        var root = new PathResolver("/", "/").Resolve(sessionRoot, "/");
        return IsInMainCheckout(root) ? PolicyDecision.Deny(PolicyRules.ProtectMainCheckout, SessionReason) : null;
    }

    /// <summary>Grund wörtlich nach Owner-Entscheid vom 26.11.2026.</summary>
    private const string SessionReason = "Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu";

    private const string AmbiguousReason = "Arbeitsverzeichnis nicht eindeutig (cd/pushd-Variante); nutze git -C <Gate-Worktree>.";

    private static PolicyDecision? ProtectMainCheckout(ToolCall call, PolicyContext ctx, List<BashAnalysis.Segment> segments, Canon paths)
    {
        if (MainCheckoutSession(call.ToolName, ctx.SessionRoot) is { } session) return session;
        if (call.ToolName is "Edit" or "Write" or "MultiEdit" or "NotebookEdit") return FileToolTargetsMainCheckout(call, ctx) ? PolicyDecision.Deny(PolicyRules.ProtectMainCheckout, FileToolReason) : null;
        if (call.ToolName != "Bash") return null;
        if (call.Command is { } text && BannedWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)))
            return PolicyDecision.Deny(PolicyRules.ProtectMainCheckout, BannedWordsReason);
        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg.Word != "git") continue;
            var sub = BashAnalysis.GitSubcommand(seg.Tokens, out var si);
            if (sub is null || Array.IndexOf(MainCheckoutWriters, sub) < 0) continue;
            if (sub == "stash" && StashReads(seg.Tokens, si)) continue;
            if (paths.AmbiguousAt(i)) return PolicyDecision.Deny(PolicyRules.ProtectMainCheckout, AmbiguousReason);
            if (TargetsMainCheckout(ctx, paths, segments, i, si)) return PolicyDecision.Deny(PolicyRules.ProtectMainCheckout, MainCheckoutReason);
        }

        return null;
    }

    /// <summary>Nur <c>git stash list</c> und <c>git stash show</c> lesen; beginnt der Rest mit einer Option, ist es ein <c>stash push</c>.</summary>
    private static bool StashReads(string[] t, int subIndex) => subIndex + 1 < t.Length && t[subIndex + 1] is "list" or "show";

    /// <summary>Vereinigung der Kandidaten: Git-Verzeichnis je Basis (nach <c>-C</c>), dazu ausdrückliche Git-Verzeichnisse und Arbeitsbäume.</summary>
    private static bool TargetsMainCheckout(PolicyContext ctx, Canon paths, List<BashAnalysis.Segment> all, int index, int subIndex)
    {
        var t = all[index].Tokens;
        var cDirs = new List<string>();
        var gitDirs = new List<string>();
        var workTrees = new List<string>();
        for (var i = 1; i < subIndex; i++)
        {
            if (t[i] == "-C" && i + 1 < t.Length) cDirs.Add(t[++i]);
            else if (t[i] == "--git-dir" && i + 1 < t.Length) gitDirs.Add(t[++i]);
            else if (t[i].StartsWith("--git-dir=", StringComparison.Ordinal)) gitDirs.Add(t[i]["--git-dir=".Length..]);
            else if (t[i] == "--work-tree" && i + 1 < t.Length) workTrees.Add(t[++i]);
            else if (t[i].StartsWith("--work-tree=", StringComparison.Ordinal)) workTrees.Add(t[i]["--work-tree=".Length..]);
            else if (BashAnalysis.GitOptionTakesValue(t[i])) i++;
        }

        foreach (var seg in all)
            for (var k = 0; k < seg.AssignmentNames.Count && k < seg.Assignments.Count; k++)
            {
                if (seg.Assignments[k].Length == 0) continue;
                if (seg.AssignmentNames[k] == "GIT_DIR") gitDirs.Add(seg.Assignments[k]);
                else if (seg.AssignmentNames[k] == "GIT_WORK_TREE") workTrees.Add(seg.Assignments[k]);
            }

        var bases = new List<string>(paths.CwdAt(index));
        foreach (var c in cDirs)
        {
            var next = new List<string>();
            foreach (var b in bases)
                foreach (var v in paths.AbsFrom(c, b))
                    if (!next.Contains(v) && next.Count < MaxRepos) next.Add(v);
            if (next.Count > 0) bases = next;
        }

        foreach (var b in bases)
            if (IsMainCheckoutGitDir(BranchGuard.FindGitDir(b))) return true;
        foreach (var dir in gitDirs)
            foreach (var b in bases)
                foreach (var abs in paths.AbsFrom(dir, b))
                    if (IsMainCheckoutGitDir(abs)) return true;
        foreach (var wt in workTrees)
            foreach (var b in bases)
                foreach (var abs in paths.AbsFrom(wt, b))
                    if (IsMainCheckoutGitDir(BranchGuard.FindGitDir(abs))) return true;
        return false;
    }

    /// <summary>Ist <paramref name="dir"/> (oder ein Ordner darüber) der Haupt-Checkout eines erweckten Projekts? Für das Briefing (HookRunner).</summary>
    public static bool IsInMainCheckout(string dir) => IsMainCheckoutGitDir(BranchGuard.FindGitDir(dir));

    /// <summary>
    /// Haupt-Checkout (ADR-0020 §1, Q-0208): <c>.git</c> ist ein Verzeichnis, der Ordner darüber hat ein Verzeichnis <c>.monarch</c>, und
    /// git-dir und git-common-dir sind nach realpath gleich, wie im git-Hook (<c>plugin/git-hooks/reference-transaction</c>). Das
    /// git-common-dir steht in <c>commondir</c> (relativ zum git-dir); fehlt die Datei, ist es das git-dir. Unklar (leer, unlesbar, Ziel fehlt)
    /// zählt als Haupt-Checkout.
    /// </summary>
    private static bool IsMainCheckoutGitDir(string? gitDir)
    {
        if (gitDir is null) return false;
        var d = gitDir.TrimEnd('/');
        try
        {
            // Ohne Beachtung der Schreibweise: auf macOS nimmt Git auch ".GIT" an.
            if (!string.Equals(Path.GetFileName(d), ".git", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(d)) return false;
            var parent = Path.GetDirectoryName(d);
            if (parent is null || !Directory.Exists(Path.Combine(parent, ".monarch"))) return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }

        return CommonDirEqualsGitDir(d);
    }

    /// <summary>realpath(git-common-dir) == realpath(git-dir); jede Unklarheit gibt true (fail closed).</summary>
    private static bool CommonDirEqualsGitDir(string gitDir)
    {
        try
        {
            var file = Path.Combine(gitDir, "commondir");
            // Ohne Datei ist git-common-dir das git-dir; ein Ordner namens commondir ist unklar. Beides zählt als Haupt-Checkout.
            if (!File.Exists(file)) return true;
            var line = File.ReadLines(file).FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(line)) return true;
            var rs = new PathResolver("/", "/");
            var real = rs.Resolve(gitDir, "/");
            var common = rs.Resolve(line, real);
            if (!Directory.Exists(common)) return true;
            return string.Equals(real.TrimEnd('/'), common.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }
}
