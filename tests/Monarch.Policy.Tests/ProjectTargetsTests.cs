using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>Q-0193: Orte, an denen der Hook nach erweckten Projekten sucht (<see cref="ProjectTargets.Locations"/>), mit Subshell-Grenzen.</summary>
public sealed class ProjectTargetsTests : IDisposable
{
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "pt-" + Guid.NewGuid().ToString("N"));
    private readonly PathResolver _rs;

    public ProjectTargetsTests()
    {
        Directory.CreateDirectory(Path.Combine(_parent, "main"));
        _rs = new PathResolver("/", _parent);
    }

    public void Dispose()
    {
        try { Directory.Delete(_parent, true); } catch (IOException) { /* best effort */ }
    }

    private IReadOnlyList<string> Locations(string cmd) =>
        ProjectTargets.Locations(new ToolCall("Bash", Command: cmd, Cwd: _parent));

    [Theory]
    [InlineData("env -C /tmp sh -c true; git -C main commit -m x")]
    [InlineData("env -C /tmp true; git -C main commit -m x")]
    [InlineData("(cd /tmp); git -C main commit -m x")]
    [InlineData("sh -c \"cd /tmp\"; git -C main commit -m x")]
    public void Git_C_after_a_subshell_is_resolved_against_the_cwd_outside_it(string cmd) =>
        Assert.Contains(_rs.Resolve("main", _rs.Cwd), Locations(cmd));

    [Fact]
    public void Cd_to_a_missing_directory_keeps_the_old_one_for_the_project_search() =>
        Assert.Contains(_rs.Resolve("main", _rs.Cwd), Locations("cd /tmp/fehlt-" + Guid.NewGuid().ToString("N") + "; git -C main commit -m x"));

    [Fact]
    public void Cd_with_an_unknown_option_keeps_the_old_directory_for_the_project_search() =>
        Assert.Contains(_rs.Resolve("main", _rs.Cwd), Locations("cd -q /tmp; git -C main commit -m x"));

    [Fact]
    public void Locations_found_before_are_kept() =>
        Assert.Contains(_rs.Resolve("main", _rs.Resolve("/tmp", _rs.Cwd)), Locations("env -C /tmp sh -c true; git -C main commit -m x"));
}
