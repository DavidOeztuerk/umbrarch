using System.Diagnostics;
using System.Text.RegularExpressions;
using Monarch.Application;
using Monarch.Domain;
using Monarch.Infrastructure.Boards;
using Monarch.Infrastructure.Boards.Frontmatter;

namespace Monarch.Cli;

/// <summary><c>monarch gate carry</c> (ADR-0020 §4): the state of merged gates and their quests comes from the gate branches into this worktree.</summary>
internal static partial class GateCarryCommands
{
    [GeneratedRegex(@"^refs/remotes/origin/monarch/(G-\d+)(?:-|$)")]
    private static partial Regex GateRef();

    private static int Refuse(CliEnv env, string message)
    {
        env.Err.WriteLine($"refused: {message}");
        return 1;
    }

    /// <summary>
    /// Takes only the field <c>state</c>: valid quest states for the quests of each merged gate, then <c>merged</c> for the gate file. Only gates that
    /// already exist here; no ledger files, no commit, no push (ADR-0018 §2: the branch on origin is not trusted for more). Repeating it changes nothing.
    /// </summary>
    public static int Carry(CliEnv env)
    {
        if (!string.IsNullOrWhiteSpace(env.GetVar("MONARCH_AGENT_TYPE")))
        {
            return Refuse(env, "gate carry is for the main session, not for a shadow.");
        }

        if (ProjectPaths.Locate(env.Cwd, env.Home) is not { } paths)
        {
            return Refuse(env, "not an awakened project.");
        }

        // ADR-0020 §4: carry runs in the new gate worktree; the main checkout is never touched (a linked worktree has a .git file, not a directory).
        if (Directory.Exists(Path.Combine(paths.Root, ".git")))
        {
            return Refuse(env, "gate carry runs in the gate worktree, not in the main checkout.");
        }

        var refs = RunGit(paths.Root, "for-each-ref", "--format=%(refname)", "refs/remotes/origin/monarch/");
        if (refs.Code != 0)
        {
            return Refuse(env, $"git for-each-ref failed: {refs.Err}");
        }

        var valid = Enum.GetValues<QuestState>().Select(QuestCommands.StateName).ToHashSet(StringComparer.Ordinal);
        var board = new LocalFileBoard(paths.Root);
        var carried = 0;
        foreach (var refName in refs.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (GateRef().Match(refName) is not { Success: true } m)
            {
                continue;
            }

            var gateId = m.Groups[1].Value;
            var gate = board.ListGatesAsync().GetAwaiter().GetResult().FirstOrDefault(g => g.Id.Value == gateId);
            if (gate is null || gate.State == "merged")
            {
                continue;
            }

            if (ReadAtRef(paths.Root, refName, ".monarch/gates", gateId) is not { } remoteGate || remoteGate.Root.GetString("state") != "merged")
            {
                continue;
            }

            var quests = new List<string>();
            foreach (var q in board.ListQuestsAsync().GetAwaiter().GetResult().Where(q => q.Gate == gate.Id && gate.Quests.Contains(q.Id)))
            {
                var state = ReadAtRef(paths.Root, refName, ".monarch/quests", q.Id.Value)?.Root.GetString("state");
                if (state is null || !valid.Contains(state) || state == QuestCommands.StateName(q.State) || board.FindQuestFile(q.Id.Value) is not { } file)
                {
                    continue;
                }

                BoardFiles.Edit(file, r => r.With("state", new YamlScalar(state)));
                quests.Add(q.Id.Value);
            }

            var gateFile = Directory.EnumerateFiles(Path.Combine(paths.MonarchDir, "gates"), gateId + "-*.md").First();
            BoardFiles.Edit(gateFile, r => r.With("state", new YamlScalar("merged")));
            var ledger = new ProjectLedger(paths, env.Clock);
            ledger.Append(ledger.SessionId(), "cli", "GateStatusCarried", ("gate", gateId), ("quests", string.Join(",", quests)), ("from_ref", refName));
            env.Out.WriteLine($"carried {gateId} from {refName}: gate merged{(quests.Count > 0 ? ", quests " + string.Join(", ", quests) : string.Empty)}.");
            carried++;
        }

        if (carried == 0)
        {
            env.Out.WriteLine("nothing to carry.");
        }

        return 0;
    }

    // The frontmatter of "<dir>/<id>-*.md" at the ref, or null.
    private static FrontmatterDocument? ReadAtRef(string root, string refName, string dir, string id)
    {
        var names = RunGit(root, "ls-tree", "--name-only", refName, "--", dir + "/");
        if (names.Code != 0)
        {
            return null;
        }

        var path = names.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(n => Path.GetFileName(n).StartsWith(id + "-", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal));
        if (path is null)
        {
            return null;
        }

        var show = RunGit(root, "show", $"{refName}:{path}");
        return show.Code == 0 ? FrontmatterDocument.Parse(show.Out + "\n") : null;
    }

    private static (int Code, string Out, string Err) RunGit(string dir, params string[] args)
    {
        if (ToolLocator.Find("git") is not { } git)
        {
            return (-1, string.Empty, "git not found");
        }

        var psi = new ProcessStartInfo(git) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(dir);
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output.Trim(), err.Result.Trim());
    }
}
