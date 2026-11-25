using System.Diagnostics;
using Monarch.Application;
using Monarch.Infrastructure.Ledger;
using Monarch.Policy;

namespace Monarch.Cli;

/// <summary><c>monarch gate cleanup</c> (ADR-0020 §6): the Monarch state of a merged gate goes onto its branch, then the worktree goes.</summary>
internal static class GateCleanupCommands
{
    private sealed record Entry(string Path, string? Branch);

    private static int Refuse(CliEnv env, string message)
    {
        env.Err.WriteLine($"refused: {message}");
        return 1;
    }

    /// <summary>
    /// From the main checkout: commits <c>.monarch</c> in the gate worktree with a skip marker, pushes the gate branch by explicit refspec and removes the
    /// worktree without <c>--force</c>. Needs GateMerged and a green IntegrationRunChecked after it; every step is safe to repeat.
    /// </summary>
    public static int Cleanup(CliEnv env, string gateId)
    {
        if (!string.IsNullOrWhiteSpace(env.GetVar("MONARCH_AGENT_TYPE")))
        {
            return Refuse(env, "gate cleanup is for the main session, not for a shadow.");
        }

        if (ProjectPaths.Locate(env.Cwd, env.Home) is not { } paths)
        {
            return Refuse(env, "not an awakened project.");
        }

        var list = RunGit(paths.Root, "worktree", "list", "--porcelain");
        if (list.Code != 0)
        {
            return Refuse(env, $"git worktree list failed: {list.Err}");
        }

        var entries = ParseWorktrees(list.Out);
        if (entries.Count == 0)
        {
            return Refuse(env, "git worktree list returned nothing.");
        }

        var main = Canon(entries[0].Path);
        var prefix = $"refs/heads/monarch/{gateId}-";
        var matches = entries.Skip(1)
            .Where(e => e.Branch is { } b && (b.StartsWith(prefix, StringComparison.Ordinal) || b == $"refs/heads/monarch/{gateId}"))
            .ToList();
        if (matches.Count == 0)
        {
            return Refuse(env, $"no worktree on a branch monarch/{gateId}-… found.");
        }

        if (matches.Count > 1)
        {
            return Refuse(env, $"more than one worktree for {gateId}: {string.Join(", ", matches.Select(m => m.Path))}.");
        }

        var wt = Canon(matches[0].Path);
        var branch = matches[0].Branch!["refs/heads/".Length..];
        if (!wt.StartsWith(Canon(Path.Combine(main, ".claude", "worktrees")) + "/", StringComparison.Ordinal))
        {
            return Refuse(env, $"{wt} is not a linked worktree under .claude/worktrees/.");
        }

        var cwd = Canon(env.Cwd);
        if (cwd == wt || cwd.StartsWith(wt + "/", StringComparison.Ordinal))
        {
            return Refuse(env, $"run gate cleanup from the main checkout ({main}), not from inside the worktree; the hook would keep writing ledger lines there.");
        }

        if (ProjectPaths.Locate(wt, env.Home) is not { } wtPaths || Canon(wtPaths.Root) != wt)
        {
            return Refuse(env, $"{wt} is not an awakened project.");
        }

        if (!LedgerVerifier.Verify(wtPaths.Root, ManaCommands.TrustedBranches(wtPaths)).Ok)
        {
            return Refuse(env, $"ledger verify has findings in {wt}; run 'monarch ledger verify' there first.");
        }

        var rows = LedgerScan.All(wtPaths.Root);
        var merged = rows.LastOrDefault(r => r.Type == "GateMerged" && r.Str("gate") == gateId);
        if (merged is null)
        {
            return Refuse(env, $"no GateMerged for {gateId} in {wt}; merge the gate first (monarch gate merge {gateId}).");
        }

        if (!rows.Any(r => r.Type == "IntegrationRunChecked" && r.Str("gate") == gateId && r.Str("result") == "green" && r.At >= merged.At))
        {
            return Refuse(env, $"no green IntegrationRunChecked for {gateId} after its merge; run 'monarch gate check {gateId}' first.");
        }

        // Written once per merge, so a second call after an interrupted one has nothing new to commit.
        if (!rows.Any(r => r.Type == "GateCleanedUp" && r.Str("gate") == gateId && r.At >= merged.At))
        {
            var ledger = new ProjectLedger(wtPaths, env.Clock);
            ledger.Append(ledger.SessionId(), "cli", "GateCleanedUp", ("gate", gateId), ("branch", branch));
        }

        var add = RunGit(wt, "add", ".monarch");
        if (add.Code != 0)
        {
            return Refuse(env, $"git add .monarch failed in {wt}: {add.Err}");
        }

        // Only the Monarch state may go into the cleanup commit; anything staged before is the user's and stays out.
        var foreign = RunGit(wt, "diff", "--cached", "--name-only").Out
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(f => !f.StartsWith(".monarch/", StringComparison.Ordinal))
            .ToList();
        if (foreign.Count > 0)
        {
            return Refuse(env, $"files outside .monarch are staged in {wt}; nothing was committed, pushed or removed: {string.Join(", ", foreign)}");
        }

        if (RunGit(wt, "diff", "--cached", "--quiet").Code != 0)
        {
            var commit = RunGit(wt, "commit", "-m", $"{gateId}: Monarch-Stand nach Merge [skip ci]");
            if (commit.Code != 0)
            {
                return Refuse(env, $"commit failed in {wt}; nothing was pushed or removed: {(commit.Err.Length > 0 ? commit.Err : commit.Out)}");
            }
        }

        var push = RunGit(wt, "push", "origin", branch);
        if (push.Code != 0)
        {
            return Refuse(env, $"push of {branch} failed; the worktree stays: {push.Err}");
        }

        var remove = RunGit(main, "worktree", "remove", wt);
        if (remove.Code != 0)
        {
            // git's own text suggests a forced removal; Monarch names the files instead (ADR-0020 §6).
            env.Err.WriteLine($"refused: git did not remove {wt}: it has modified or untracked files, or it is locked. The Monarch state is on {branch}.");
            env.Err.WriteLine("Files that keep the worktree:");
            env.Err.WriteLine(RunGit(wt, "status", "--porcelain", "-uall").Out);
            return 1;
        }

        env.Out.WriteLine($"cleaned up {gateId}: Monarch state pushed to {branch}, worktree {wt} removed.");
        return 0;
    }

    private static List<Entry> ParseWorktrees(string porcelain)
    {
        var result = new List<Entry>();
        string? path = null;
        string? branch = null;
        foreach (var line in porcelain.Split('\n').Append(string.Empty))
        {
            if (line.Length == 0)
            {
                if (path is not null)
                {
                    result.Add(new Entry(path, branch));
                }

                path = null;
                branch = null;
            }
            else if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                path = line["worktree ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..];
            }
        }

        return result;
    }

    // git prints real paths (macOS: /private/var/…); compare canonical paths only.
    private static string Canon(string path) => PathCanon.Resolve("/", Path.GetFullPath(path)).TrimEnd('/');

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
