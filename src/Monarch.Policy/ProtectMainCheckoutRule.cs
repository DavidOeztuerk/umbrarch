namespace Monarch.Policy;

/// <summary>Regel <c>protect_main_checkout</c> (ADR-0020 Abschnitt 1): keine schreibenden Git-Befehle im Haupt-Checkout eines erweckten Projekts.</summary>
public static partial class ToolCallPolicy
{
    private static readonly string[] MainCheckoutWriters = ["checkout", "switch", "reset", "merge", "pull", "commit", "rebase", "stash"];

    private const string MainCheckoutReason =
        "Der Haupt-Checkout bleibt unverändert (Standardregel 2). Arbeite in der Gate-Worktree: "
        + "`git worktree add .claude/worktrees/<G> -b monarch/<G>-<slug> origin/develop`, dann EnterWorktree.";

    private static PolicyDecision? ProtectMainCheckout(ToolCall call, PolicyContext ctx, List<BashAnalysis.Segment> segments, Canon paths)
    {
        if (call.ToolName != "Bash") return null;
        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg.Word != "git") continue;
            var sub = BashAnalysis.GitSubcommand(seg.Tokens, out var si);
            if (sub is null || Array.IndexOf(MainCheckoutWriters, sub) < 0) continue;
            if (sub == "stash" && StashReads(seg.Tokens, si)) continue;
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

    /// <summary>Haupt-Checkout: <c>.git</c> ist ein Verzeichnis ohne <c>commondir</c>, und der Ordner darüber hat ein Verzeichnis <c>.monarch</c>.</summary>
    private static bool IsMainCheckoutGitDir(string? gitDir)
    {
        if (gitDir is null) return false;
        var d = gitDir.TrimEnd('/');
        try
        {
            // Ohne Beachtung der Schreibweise: auf macOS nimmt Git auch ".GIT" an.
            if (!string.Equals(Path.GetFileName(d), ".git", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(d) || File.Exists(Path.Combine(d, "commondir"))) return false;
            var parent = Path.GetDirectoryName(d);
            return parent is not null && Directory.Exists(Path.Combine(parent, ".monarch"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
