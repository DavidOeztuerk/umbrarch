using System.Diagnostics;
using System.Text.RegularExpressions;
using Monarch.Application;
using Monarch.Policy;

namespace Monarch.Cli;

/// <summary><c>monarch gate open</c> (ADR-0021 §2, #166): fetches origin and adds the gate worktree from the integration branch, for the owner's own terminal.</summary>
internal static partial class GateOpenCommands
{
    private static int Refuse(CliEnv env, string message)
    {
        env.Err.WriteLine($"refused: {message}");
        return 1;
    }

    /// <summary>Prints only the absolute path of the new worktree on stdout, so that a shell can change into it; git's own output goes to stderr.</summary>
    public static int Open(CliEnv env, string gateId, string slug)
    {
        // In the sandbox worktree add fails halfway and leaves the branch behind (ADR-0021 §2, probe).
        if (!string.IsNullOrEmpty(env.GetVar("CLAUDE" + "CODE")))
        {
            return Refuse(env, "gate open runs in the owner's own terminal, not in a Claude Code session (ADR-0021 §2).");
        }

        if (!GateIdPattern().IsMatch(gateId))
        {
            return Refuse(env, $"'{gateId}' is not a gate id (G-0000).");
        }

        if (slug.Length > 60 || !SlugPattern().IsMatch(slug))
        {
            return Refuse(env, $"'{slug}' is not a slug (lowercase letters and digits, joined by single hyphens, at most 60 characters).");
        }

        if (ProjectPaths.Locate(env.Cwd, env.Home) is not { } paths)
        {
            return Refuse(env, "not an awakened project.");
        }

        var list = RunGit(paths.Root, "worktree", "list", "--porcelain");
        var main = list.Out.Split('\n').FirstOrDefault(l => l.StartsWith("worktree ", StringComparison.Ordinal))?["worktree ".Length..];
        if (list.Code != 0 || string.IsNullOrEmpty(main))
        {
            return Refuse(env, $"git worktree list failed: {list.Err}");
        }

        var integration = SystemConfigReader.Load(paths.Root).IntegrationBranch;
        var fetch = RunGit(main, "fetch", "origin");
        Forward(env, fetch);
        if (fetch.Code != 0)
        {
            return 1;
        }

        var wt = Path.Combine(main, ".claude", "worktrees", gateId);
        var add = RunGit(main, "worktree", "add", "--no-track", wt, "-b", $"monarch/{gateId}-{slug}", $"origin/{integration}");
        Forward(env, add);
        if (add.Code != 0)
        {
            return 1;
        }

        env.Out.WriteLine(wt);
        return 0;
    }

    private static void Forward(CliEnv env, (int Code, string Out, string Err) r)
    {
        foreach (var text in new[] { r.Out, r.Err }.Where(t => t.Length > 0))
        {
            env.Err.WriteLine(text);
        }
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

    [GeneratedRegex(@"^G-\d{4,}$")]
    private static partial Regex GateIdPattern();

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
