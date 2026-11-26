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

    /// <summary>Zielpfad per realpath auflösen (fehlende Komponenten bleiben lexikalisch), dann das Repo des Zielverzeichnisses bestimmen (<see cref="IsInMainCheckout"/>).</summary>
    private static bool FileToolTargetsMainCheckout(ToolCall call, PolicyContext ctx)
    {
        if (string.IsNullOrEmpty(call.FilePath)) return false;
        var rs = new PathResolver(ctx.ProjectRoot, call.Cwd);
        var abs = rs.Resolve(PathResolver.ExpandTilde(call.FilePath), rs.Cwd);
        var dir = Path.GetDirectoryName(abs);
        return dir is not null && IsInMainCheckout(dir);
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
            if (IsInMainCheckout(b)) return true;
        foreach (var dir in gitDirs)
            foreach (var b in bases)
                foreach (var abs in paths.AbsFrom(dir, b))
                    if (IsMainCheckoutGitDir(abs)) return true;
        foreach (var wt in workTrees)
            foreach (var b in bases)
                foreach (var abs in paths.AbsFrom(wt, b))
                    if (IsInMainCheckout(abs)) return true;
        return false;
    }

    /// <summary>
    /// Liegt <paramref name="dir"/> im Haupt-Checkout eines erweckten Projekts (ADR-0020 §1, Q-0213)? Nach realpath gilt:
    /// (1) Die oberste Pfad-Komponente <c>.git</c>, die ein Ordner ist, bestimmt das Repo; ein inneres <c>.git</c> verdeckt kein äußeres.
    /// (2) Sonst vom Ordner aufwärts: ein Ordner <c>.git</c> bestimmt das Repo; eine Datei <c>.git</c> ist nur dann die Grenze, wenn sie auf
    /// das git-dir einer echten Worktree zeigt (<see cref="IsLinkedWorktreeGitDir"/>); sonst geht die Suche weiter nach oben.
    /// Haupt-Checkout heißt: der Ordner über dem <c>.git</c> hat <c>.monarch</c>, und das <c>.git</c> ist kein git-dir einer echten Worktree.
    /// Auch für das Briefing (HookRunner) und Option B.
    /// </summary>
    public static bool IsInMainCheckout(string dir)
    {
        try
        {
            var real = new PathResolver("/", "/").Resolve(dir, "/");
            var cur = string.Empty;
            foreach (var part in real.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                cur += "/" + part;
                // Ohne Beachtung der Schreibweise: auf macOS nimmt Git auch ".GIT" an.
                if (part.Equals(".git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(cur)) return IsMainRepo(cur);
            }

            for (var d = real; !string.IsNullOrEmpty(d); d = Path.GetDirectoryName(d))
            {
                var dotGit = Path.Combine(d, ".git");
                if (Directory.Exists(dotGit)) return IsMainRepo(dotGit);
                if (File.Exists(dotGit) && GitFileTarget(dotGit, d) is { } target && IsLinkedWorktreeGitDir(target)) return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }

        return false;
    }

    /// <summary>Ausdrückliches git-dir (<c>--git-dir</c>, <c>GIT_DIR</c>): Haupt-Checkout, wenn es nach realpath ein Ordner <c>.git</c> ist (<see cref="IsMainRepo"/>).</summary>
    private static bool IsMainCheckoutGitDir(string? gitDir)
    {
        if (gitDir is null) return false;
        try
        {
            var d = new PathResolver("/", "/").Resolve(gitDir, "/").TrimEnd('/');
            return string.Equals(Path.GetFileName(d), ".git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(d) && IsMainRepo(d);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Ordner <c>.git</c> (realpath): Haupt-Checkout, wenn der Ordner darüber <c>.monarch</c> hat und es kein git-dir einer echten Worktree ist.</summary>
    private static bool IsMainRepo(string dotGitDir)
    {
        var parent = Path.GetDirectoryName(dotGitDir.TrimEnd('/'));
        return parent is not null && Directory.Exists(Path.Combine(parent, ".monarch")) && !IsLinkedWorktreeGitDir(dotGitDir);
    }

    /// <summary>
    /// git-dir einer echten Worktree (Q-0213, Minor 2): <c>commondir</c> ist lesbar, das git-common-dir existiert, und das git-dir liegt nach
    /// realpath direkt in <c>&lt;common&gt;/worktrees/</c>. Alles andere, auch jede Unklarheit, ist keine Worktree (fail closed).
    /// </summary>
    private static bool IsLinkedWorktreeGitDir(string gitDir)
    {
        try
        {
            var rs = new PathResolver("/", "/");
            var real = rs.Resolve(gitDir, "/").TrimEnd('/');
            var file = Path.Combine(real, "commondir");
            if (!File.Exists(file)) return false;
            var line = File.ReadLines(file).FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(line)) return false;
            var common = rs.Resolve(line, real).TrimEnd('/');
            if (!Directory.Exists(common)) return false;
            return string.Equals(Path.GetDirectoryName(real), common + "/worktrees", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Ziel der Zeile <c>gitdir:</c> einer Datei <c>.git</c>, relativ zu ihrem Ordner; null ohne solche Zeile.</summary>
    private static string? GitFileTarget(string dotGitFile, string dir)
    {
        foreach (var line in File.ReadLines(dotGitFile))
            if (line.StartsWith("gitdir:", StringComparison.Ordinal)) return Path.GetFullPath(line["gitdir:".Length..].Trim(), dir);
        return null;
    }
}
