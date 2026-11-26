namespace Monarch.Cli.Tests;

/// <summary>
/// Q-0216 / ADR-0021 §1 (#167): which checks make develop green for <c>gate merge</c>. R1: checks of scheduled (nightly) workflow runs do not count.
/// R3: <c>evidence.required</c> narrows the set to the required checks of the branch protection. Fake GitHub, no network.
/// </summary>
public class DevelopGreenTests
{
    private const string Review = "https://example.test/review/1";
    private const string RequiredYaml = "evidence:\n  required: [\"build-test (ubuntu-latest)\", \"plugin\", \"aot (linux-x64)\"]\n";
    private const long PrRun = 800;
    private const long Push = 900;
    private const long OldNightly = 901;
    private const long Nightly = 902;

    private static (TempProject P, FakeForge F) Setup(bool required)
    {
        var p = new TempProject(systemYaml: Seed.ForgeYaml + "git:\n  merge_method: squash\n" + (required ? RequiredYaml : ""));
        var f = new FakeForge();
        f.Pulls[7] = (FakeForge.Sha, false);
        f.Checks[FakeForge.Sha] = FakeForge.ChecksOf(Green(PrRun, 1, 0));
        f.WorkflowRuns[FakeForge.Sha] = FakeForge.RunsOf((PrRun, "pull_request"));
        f.Checks[FakeForge.MergeSha] = f.Checks[FakeForge.Sha];
        f.WorkflowRuns[FakeForge.MergeSha] = f.WorkflowRuns[FakeForge.Sha];
        p.Http = f;
        Seed.Gate(p, "G-0001", "[Q-0001]", "C");
        Seed.Quest(p, "Q-0001", "in_review", rank: "C", extra: "board: { provider: github, pr: 7 }\n");
        return (p, f);
    }

    /// <summary>The four jobs of a push or pull_request run of ci.yml on Linux, all green.</summary>
    private static string[] Green(long suite, int firstId, int minute) =>
    [
        FakeForge.RunIn(suite, firstId, "plan", "success", minute),
        FakeForge.RunIn(suite, firstId + 1, "build-test (ubuntu-latest)", "success", minute + 1),
        FakeForge.RunIn(suite, firstId + 2, "plugin", "success", minute + 1),
        FakeForge.RunIn(suite, firstId + 3, "aot (linux-x64)", "success", minute + 2),
    ];

    private static void Approved(TempProject p)
    {
        p.Clock.UtcNow = p.Clock.UtcNow.AddMinutes(1);
        Assert.Equal(0, p.Hook("subagent-stop", """{"session_id":"sess1","hook_event_name":"SubagentStop","agent_id":"a1","agent_type":"umbrarch:warden"}""").Code);
        p.Clock.UtcNow = p.Clock.UtcNow.AddMinutes(1);
        Assert.Equal(0, p.Run(["gate", "verdict", "G-0001", "--verdict", "approve", "--source", Review]).Code);
    }

    private static RunResult Merge(TempProject p)
    {
        p.Clock.UtcNow = p.Clock.UtcNow.AddMinutes(1);
        return p.Run(["gate", "merge", "G-0001"], vars: new Dictionary<string, string?> { ["MONARCH_MERGE_WAIT_SECONDS"] = "0", ["MONARCH_MERGE_POLL_SECONDS"] = "0" });
    }

    private static void Merged(FakeForge f, RunResult r)
    {
        Assert.True(r.Code == 0, r.Err);
        Assert.Single(f.MergeBodies);
    }

    private static void Refused(FakeForge f, RunResult r, string reason)
    {
        Assert.Equal(1, r.Code);
        Assert.StartsWith("refused:", r.Err, StringComparison.Ordinal);
        Assert.Contains(reason, r.Err, StringComparison.Ordinal);
        Assert.Empty(f.MergeBodies);
    }

    /// <summary>
    /// Boss, the case of #167 at dfa5664: an old nightly run with the unresolved matrix placeholder (skipped) and a cancelled macOS job, a newer green
    /// nightly run and a green push run. Without evidence.required only R1 removes the placeholder; with R3 it is not a required name.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_ghost_check_of_an_old_nightly_run_does_not_make_develop_red(bool required)
    {
        var (p, f) = Setup(required);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf(
            [
                .. Green(Push, 1, 0),
                FakeForge.RunIn(OldNightly, 11, "plan", "success", 10),
                FakeForge.RunIn(OldNightly, 12, "plugin", "success", 10),
                FakeForge.RunIn(OldNightly, 13, "build-test (ubuntu-latest)", "success", 12),
                FakeForge.RunIn(OldNightly, 14, "build-test (macos-latest)", "cancelled", 13),
                FakeForge.RunIn(OldNightly, 15, "aot (${{ matrix.rid }})", "skipped", 13),
                .. Green(Nightly, 21, 20),
                FakeForge.RunIn(Nightly, 25, "build-test (macos-latest)", "success", 22),
                FakeForge.RunIn(Nightly, 26, "aot (osx-arm64)", "success", 23),
            ]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"), (OldNightly, "schedule"), (Nightly, "schedule"));
            Approved(p);
            Merged(f, Merge(p));
            Assert.Contains(f.Paths, x => x == "GET /repos/o/r/actions/runs");
        }
    }

    /// <summary>R1: a red check that only a nightly run reports does not make develop red when the push run is green.</summary>
    [Fact]
    public void A_red_nightly_run_does_not_count_when_the_push_run_is_green()
    {
        var (p, f) = Setup(required: false);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf([.. Green(Push, 1, 0), FakeForge.RunIn(Nightly, 21, "build-test (ubuntu-latest)", "failure", 30)]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"), (Nightly, "schedule"));
            Approved(p);
            Merged(f, Merge(p));
        }
    }

    /// <summary>R1 is stricter as well: a later green nightly run no longer covers a red push run.</summary>
    [Fact]
    public void A_red_push_run_stays_red_after_a_later_green_nightly_run()
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf(
            [
                FakeForge.RunIn(Push, 1, "plan", "success", 0),
                FakeForge.RunIn(Push, 2, "build-test (ubuntu-latest)", "failure", 1),
                FakeForge.RunIn(Push, 3, "plugin", "success", 1),
                FakeForge.RunIn(Push, 4, "aot (linux-x64)", "success", 2),
                .. Green(Nightly, 21, 20),
            ]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"), (Nightly, "schedule"));
            Approved(p);
            var r = Merge(p);
            Refused(f, r, "not green");
            Assert.Contains("build-test (ubuntu-latest)=failure", r.Err, StringComparison.Ordinal);
        }
    }

    /// <summary>R1 with R3: a required check that only a nightly run reports is missing, so develop counts as still running.</summary>
    [Fact]
    public void A_required_check_only_from_a_nightly_run_is_missing()
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf(
            [
                FakeForge.RunIn(Push, 1, "plan", "success", 0),
                FakeForge.RunIn(Push, 2, "build-test (ubuntu-latest)", "success", 1),
                FakeForge.RunIn(Push, 3, "plugin", "success", 1),
                .. Green(Nightly, 21, 20),
            ]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"), (Nightly, "schedule"));
            Approved(p);
            var r = Merge(p);
            Refused(f, r, "develop läuft noch");
            Assert.Contains("aot (linux-x64)", r.Err, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Guard (ADR-0021 Offen 3): with R3 a red build-test leaves aot only as the matrix placeholder, so a required check is missing and the merge
    /// is refused as still running, also for a proven fix PR. Before R3 the placeholder blocked the fix PR as well (it is missing at the PR head).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_red_build_test_with_R3_refuses_the_merge_also_for_a_fix_pr(bool provenFix)
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf(
                FakeForge.RunIn(Push, 1, "plan", "success", 0),
                FakeForge.RunIn(Push, 2, "build-test (ubuntu-latest)", "failure", 1),
                FakeForge.RunIn(Push, 3, "plugin", "success", 1),
                FakeForge.RunIn(Push, 4, "aot (${{ matrix.rid }})", "skipped", 2));
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"));
            if (provenFix)
            {
                f.Compare[$"{FakeForge.DevelopSha}...{FakeForge.Sha}"] = "ahead";
                f.Parents[FakeForge.MergeSha] = FakeForge.DevelopSha;
            }

            Approved(p);
            var r = Merge(p);
            Refused(f, r, "develop läuft noch");
            Assert.Contains("aot (linux-x64)", r.Err, StringComparison.Ordinal);
        }
    }

    /// <summary>A still running nightly run does not hold develop as running (R1 covers runs that are not completed).</summary>
    [Fact]
    public void A_running_nightly_run_does_not_hold_develop()
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            var running = $$$"""{"id":31,"name":"build-test (ubuntu-latest)","head_sha":"{{{FakeForge.Sha}}}","status":"queued","conclusion":null,"completed_at":null,"html_url":"https://github.com/o/r/actions/runs/{{{Nightly}}}/job/31","check_suite":{"id":{{{Nightly}}}},"app":{"slug":"github-actions"}}""";
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf([.. Green(Push, 1, 0), running]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"), (Nightly, "schedule"));
            Approved(p);
            Merged(f, Merge(p));
        }
    }

    /// <summary>
    /// Warden major 1: an Actions check whose suite is missing in actions/runs (race, deleted run) cannot be told apart from a nightly one.
    /// It must not cover a red push run; the result is pending (fail-closed) and names the check and its suite.
    /// </summary>
    [Fact]
    public void An_actions_check_with_an_unknown_suite_holds_develop()
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf(
            [
                FakeForge.RunIn(Push, 1, "plan", "success", 0),
                FakeForge.RunIn(Push, 2, "build-test (ubuntu-latest)", "failure", 1),
                FakeForge.RunIn(Push, 3, "plugin", "success", 1),
                FakeForge.RunIn(Push, 4, "aot (linux-x64)", "success", 2),
                FakeForge.RunIn(Nightly, 22, "build-test (ubuntu-latest)", "success", 21),
            ]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"));
            Approved(p);
            var r = Merge(p);
            Refused(f, r, "develop läuft noch");
            Assert.Contains(Nightly.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Err, StringComparison.Ordinal);
        }
    }

    /// <summary>Guard: a check of another app has no workflow run and keeps counting (R1 does not touch it).</summary>
    [Fact]
    public void A_red_check_without_a_workflow_run_still_counts()
    {
        var (p, f) = Setup(required: false);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf([.. Green(Push, 1, 0), FakeForge.Run(40, "external-lint", "failure")]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "push"));
            Approved(p);
            Refused(f, Merge(p), "not green");
        }
    }

    /// <summary>Guard for R3: a red check outside evidence.required does not block; a red required check does.</summary>
    [Theory]
    [InlineData("build-test (macos-latest)", true)]
    [InlineData("plugin", false)]
    public void Only_required_checks_decide(string red, bool merges)
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            var runs = Green(Push, 1, 0).Select(r => r.Contains($"\"name\":\"{red}\"", StringComparison.Ordinal) ? r.Replace("\"success\"", "\"failure\"", StringComparison.Ordinal) : r).ToList();
            if (!runs.Any(r => r.Contains($"\"name\":\"{red}\"", StringComparison.Ordinal)))
            {
                runs.Add(FakeForge.RunIn(Push, 9, red, "failure", 3));
            }

            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf([.. runs]);
            f.WorkflowRuns[FakeForge.DevelopSha] = FakeForge.RunsOf((Push, "workflow_dispatch"));
            Approved(p);
            var r = Merge(p);
            if (merges)
            {
                Merged(f, r);
            }
            else
            {
                Refused(f, r, "not green");
            }
        }
    }

    /// <summary>Without the events of the workflow runs a nightly check cannot be told apart: fail-closed.</summary>
    [Fact]
    public void A_failing_workflow_run_lookup_refuses_the_merge()
    {
        var (p, f) = Setup(required: true);
        using (p)
        {
            f.Checks[FakeForge.DevelopSha] = FakeForge.ChecksOf(Green(Push, 1, 0));
            Approved(p);
            f.WorkflowRunsFailWith = 500;
            Refused(f, Merge(p), "merge failed");
        }
    }

    /// <summary>R3 as the owner entered it (.monarch/system.yaml:30-31): a flow list with parentheses and spaces yields exactly the three names.</summary>
    [Fact]
    public void Required_reads_the_three_names_of_the_flow_list()
    {
        using var p = new TempProject(systemYaml: Seed.ForgeYaml + RequiredYaml);
        var paths = ProjectPaths.Locate(p.Root, p.Home)!;
        Assert.Equal(["build-test (ubuntu-latest)", "plugin", "aot (linux-x64)"], Forge.Required(paths));
    }
}
