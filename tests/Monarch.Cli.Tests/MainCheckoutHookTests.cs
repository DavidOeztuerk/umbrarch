using System.Text.Json;

namespace Monarch.Cli.Tests;

/// <summary>ADR-0020 §1: SessionStart-Warnung im Haupt-Checkout (Schicht 3) und Datei-Tools über Symlinks im echten Hook.</summary>
public class MainCheckoutHookTests
{
    private const string Warning = "Warnung: Diese Session läuft im Haupt-Checkout. Bash und alle Datei-Schreibwerkzeuge sind hier gesperrt (ADR-0020 §1, Option B). Starte die Session in der Gate-Worktree neu.";

    private static string Briefing(TempProject p)
    {
        var r = p.Hook("session-start", """{"session_id":"s1","hook_event_name":"SessionStart"}""");
        return JsonDocument.Parse(r.Out).RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()!;
    }

    [Fact]
    public void Session_start_in_the_main_checkout_warns()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        Assert.Contains(Warning, Briefing(p), StringComparison.Ordinal);
    }

    [Fact]
    public void Session_start_in_a_worktree_does_not_warn()
    {
        using var p = new TempProject(activeRank: "A");
        var gitDir = Path.Combine(p.Root, ".git-wt");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(p.Root, ".git"), "gitdir: " + gitDir + "\n");
        Assert.DoesNotContain("Warnung", Briefing(p), StringComparison.Ordinal);
    }

    /// <summary>Q-0213 (Owner-Entscheid 2, Minor 2): Ein .git-Ordner mit commondir ist nur dann eine Worktree, wenn er unter &lt;common&gt;/worktrees/ liegt.
    /// Bis Q-0212 hieß dieser Test ..._does_not_warn; das war die Lücke aus Minor 2.</summary>
    [Fact]
    public void Session_start_with_a_git_dir_whose_commondir_points_elsewhere_warns()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        File.WriteAllText(Path.Combine(p.Root, ".git", "commondir"), "../..\n");
        Assert.Contains(Warning, Briefing(p), StringComparison.Ordinal);
    }

    /// <summary>Q-0213: Der echte Hook verweigert Write auf .git/packed-refs auch mit einem inneren .git-Ordner.</summary>
    [Fact]
    public void Write_into_the_main_git_dir_with_an_inner_git_dir_is_denied_by_the_hook()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git", ".git"));
        var r = p.Hook("pre-tool-use", TempProject.PreToolUse("Write", file: Path.Combine(p.Root, ".git", "packed-refs")));
        Assert.Contains("\"deny\"", r.Out, StringComparison.Ordinal);
        Assert.Contains("protect_main_checkout", string.Join('\n', p.Events().Select(e => e.Payload.ToString())), StringComparison.Ordinal);
    }

    /// <summary>Q-0208: commondir mit Ziel auf das eigene .git (git-dir == git-common-dir) ist weiter der Haupt-Checkout.</summary>
    [Fact]
    public void Session_start_with_commondir_pointing_at_its_own_git_dir_warns()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        File.WriteAllText(Path.Combine(p.Root, ".git", "commondir"), ".\n");
        Assert.Contains(Warning, Briefing(p), StringComparison.Ordinal);
    }

    /// <summary>Q-0208: Der echte Hook verweigert Write im Haupt-Checkout auch mit commondir = ".".</summary>
    [Fact]
    public void Write_into_the_main_checkout_with_commondir_dot_is_denied_by_the_hook()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        File.WriteAllText(Path.Combine(p.Root, ".git", "commondir"), ".\n");
        var r = p.Hook("pre-tool-use", TempProject.PreToolUse("Write", file: Path.Combine(p.Root, "src", "a.cs")));
        Assert.Contains("\"deny\"", r.Out, StringComparison.Ordinal);
        Assert.Contains("protect_main_checkout", string.Join('\n', p.Events().Select(e => e.Payload.ToString())), StringComparison.Ordinal);
    }

    private const string SessionReason = "Session im Haupt-Checkout gestartet; starte sie in der Gate-Worktree neu";

    private static Dictionary<string, string?> ProjectDir(string dir) => new() { ["CLAUDE_PROJECT_DIR"] = dir };

    /// <summary>Q-0209 (Option B): CLAUDE_PROJECT_DIR im Haupt-Checkout, der echte Hook verweigert Bash und Write, Read bleibt.</summary>
    [Fact]
    public void Hook_denies_bash_and_write_when_the_session_started_in_the_main_checkout()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        var bash = p.Run(["hook", "pre-tool-use"], TempProject.PreToolUse("Bash", command: "ls"), vars: ProjectDir(p.Root));
        Assert.Contains("\"deny\"", bash.Out, StringComparison.Ordinal);
        Assert.Contains(SessionReason, bash.Out, StringComparison.Ordinal);
        var write = p.Run(["hook", "pre-tool-use"], TempProject.PreToolUse("Write", file: Path.Combine(p.Root, "x.txt")), vars: ProjectDir(p.Root));
        Assert.Contains(SessionReason, write.Out, StringComparison.Ordinal);
        var read = p.Run(["hook", "pre-tool-use"], TempProject.PreToolUse("Read", file: Path.Combine(p.Root, "README.md")), vars: ProjectDir(p.Root));
        Assert.DoesNotContain(SessionReason, read.Out, StringComparison.Ordinal);
        Assert.Contains("protect_main_checkout", string.Join('\n', p.Events().Select(e => e.Payload.ToString())), StringComparison.Ordinal);
    }

    /// <summary>Auch wenn das cwd außerhalb jedes erweckten Projekts liegt (cd /tmp), bleibt die Sperre.</summary>
    [Fact]
    public void Hook_denies_bash_from_a_main_checkout_session_even_with_cwd_outside_any_project()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        var outside = Path.Combine(Path.GetTempPath(), "monarch-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            var r = p.Run(["hook", "pre-tool-use"], TempProject.PreToolUse("Bash", command: "ls"), cwd: outside, vars: ProjectDir(p.Root));
            Assert.Contains("\"deny\"", r.Out, StringComparison.Ordinal);
            Assert.Contains(SessionReason, r.Out, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public void Hook_without_project_dir_or_with_a_worktree_root_does_not_apply_option_b()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        Assert.DoesNotContain(SessionReason, p.Run(["hook", "pre-tool-use"], TempProject.PreToolUse("Bash", command: "ls")).Out, StringComparison.Ordinal);
        using var wt = new TempProject(activeRank: "A");
        var gitDir = Path.Combine(p.Root, ".git", "worktrees", "G-1");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..\n");
        File.WriteAllText(Path.Combine(wt.Root, ".git"), "gitdir: " + gitDir + "\n");
        Assert.DoesNotContain(SessionReason, wt.Run(["hook", "pre-tool-use"], TempProject.PreToolUse("Bash", command: "ls"), vars: ProjectDir(wt.Root)).Out, StringComparison.Ordinal);
    }

    /// <summary>Belegt, dass der Hook den Haupt-Checkout bei Datei-Tools über einen Symlink als Ziel-Projekt findet.</summary>
    [Fact]
    public void Write_through_a_symlink_into_the_main_checkout_is_denied_by_the_hook()
    {
        using var main = new TempProject(activeRank: "A");
        using var other = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(main.Root, ".git"));
        Directory.CreateDirectory(Path.Combine(main.Root, "src"));
        File.WriteAllText(Path.Combine(other.Root, ".git"), "gitdir: " + Path.Combine(main.Root, ".git") + "\n");
        var lnk = Path.Combine(other.Root, "lnk");
        Directory.CreateSymbolicLink(lnk, Path.Combine(main.Root, "src"));
        var json = TempProject.PreToolUse("Write", file: Path.Combine(lnk, "a.cs"));
        var r = other.Run(["hook", "pre-tool-use"], json, cwd: other.Root);
        Assert.Contains("\"deny\"", r.Out, StringComparison.Ordinal);
        Assert.Contains("protect_main_checkout", string.Join('\n', main.Events().Select(e => e.Payload.ToString())), StringComparison.Ordinal);
    }
}
