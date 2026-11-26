using System.Text.Json;

namespace Monarch.Cli.Tests;

/// <summary>ADR-0020 §1: SessionStart-Warnung im Haupt-Checkout (Schicht 3) und Datei-Tools über Symlinks im echten Hook.</summary>
public class MainCheckoutHookTests
{
    private const string Warning = "Warnung: Diese Session läuft im Haupt-Checkout. Die Sandbox gibt hier den ganzen Haupt-Checkout zum Schreiben frei (ADR-0020 §1). Arbeite in der Gate-Worktree.";

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

    [Fact]
    public void Session_start_with_a_git_dir_that_has_commondir_does_not_warn()
    {
        using var p = new TempProject(activeRank: "A");
        Directory.CreateDirectory(Path.Combine(p.Root, ".git"));
        File.WriteAllText(Path.Combine(p.Root, ".git", "commondir"), "../..\n");
        Assert.DoesNotContain("Warnung", Briefing(p), StringComparison.Ordinal);
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
