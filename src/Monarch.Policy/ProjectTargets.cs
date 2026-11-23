namespace Monarch.Policy;

/// <summary>Findet die Orte, an denen ein Tool-Call wirken kann; der Hook sucht dort nach erweckten Projekten.</summary>
public static class ProjectTargets
{
    /// <summary>
    /// Kanonische absolute Pfade, von denen aus ein Projekt gesucht werden soll: das cwd, bei Datei-Tools der Zielpfad, bei Bash die Ziele von
    /// <c>cd</c>/<c>pushd</c>/<c>git -C</c>/<c>--git-dir</c>/<c>--work-tree</c> sowie absolute Pfad-Tokens (auch <c>~</c>) des Kommandos.
    /// </summary>
    public static IReadOnlyList<string> Locations(ToolCall call)
    {
        var rs = new PathResolver("/", string.IsNullOrEmpty(call.Cwd) ? "/" : call.Cwd);
        var list = new List<string>();
        void Add(string p) { if (!list.Contains(p)) list.Add(p); }
        Add(rs.Cwd);
        if (call.FilePath is { Length: > 0 } fp) Add(rs.Resolve(PathResolver.ExpandTilde(fp), rs.Cwd));
        if (call.ToolName != "Bash" || call.Command is not { } cmd) return list;
        var segments = BashAnalysis.Segments(cmd);
        var cur = rs.Cwd;
        // Zweiter Cursor wie das cwd-Modell (ToolCallPolicy.Canon): nach einer Subshell gilt wieder das Verzeichnis davor.
        var scoped = rs.Cwd;
        var saved = new Stack<string>();
        var bases = new List<string> { rs.Cwd };
        foreach (var seg in segments)
        {
            foreach (var ev in seg.Events)
            {
                if (ev == '(') saved.Push(scoped);
                else if (saved.Count > 0) scoped = saved.Pop();
            }

            foreach (var (dir, changes) in BashAnalysis.DirectoryTargets([seg]))
            {
                var expanded = PathResolver.ExpandTilde(dir);
                var d = rs.Resolve(expanded, cur);
                var s = rs.Resolve(expanded, scoped);
                Add(d);
                Add(s);
                if (!bases.Contains(d)) bases.Add(d);
                if (!bases.Contains(s)) bases.Add(s);
                if (changes)
                {
                    cur = d;
                    scoped = s;
                }
            }
        }

        foreach (var token in BashAnalysis.PathTokens(cmd))
        {
            if (token.StartsWith('/') || token == "~" || token.StartsWith("~/", StringComparison.Ordinal))
            {
                Add(rs.Resolve(PathResolver.ExpandTilde(token), rs.Cwd));
            }
            else if (token is "." or ".." || token.Contains('/'))
            {
                // Relative tokens also search for projects (ADR-0011 rule 6): against the cwd and every cd target of the command.
                foreach (var b in bases) Add(rs.Resolve(token, b));
            }
        }

        return list;
    }
}
