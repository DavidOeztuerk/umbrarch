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
        // Zwei Mengen wie das cwd-Modell (ToolCallPolicy.Canon): nach einer Subshell gilt wieder die Menge davor.
        var cur = new List<string> { rs.Cwd };
        var scoped = new List<string> { rs.Cwd };
        var visited = new List<string> { rs.Cwd };
        var saved = new Stack<List<string>>();
        var bases = new List<string> { rs.Cwd };
        foreach (var seg in segments)
        {
            foreach (var ev in seg.Events)
            {
                if (ev == '(') saved.Push(scoped);
                else if (saved.Count > 0) scoped = saved.Pop();
            }

            // Wo cd/pushd/popd unklar enden (Option, fehlendes Ziel, Stapelwahl), gilt die Vereinigung aus altem und neuem Verzeichnis.
            var dirArgs = seg.Word is "cd" or "pushd" or "popd" ? BashAnalysis.ParseDirArgs(seg) : null;
            if (seg.Word == "popd" || dirArgs is { StackSelect: true })
            {
                cur = Merge(cur, visited);
                scoped = Merge(scoped, visited);
            }

            foreach (var (dir, changes) in BashAnalysis.DirectoryTargets([seg]))
            {
                var expanded = PathResolver.ExpandTilde(dir);
                var d = cur.Select(c => rs.Resolve(expanded, c)).Distinct().ToList();
                var s = scoped.Select(c => rs.Resolve(expanded, c)).Distinct().ToList();
                foreach (var p in d.Concat(s))
                {
                    Add(p);
                    if (!bases.Contains(p)) bases.Add(p);
                }

                if (!changes) continue;
                var keep = dirArgs is { UncertainOption: true } or { Ambiguous: true } || !d.Concat(s).Any(Directory.Exists);
                cur = keep ? Merge(cur, d) : d;
                scoped = keep ? Merge(scoped, s) : s;
                visited = Merge(visited, cur);
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

    private static List<string> Merge(List<string> a, List<string> b) => [.. a.Union(b)];
}
