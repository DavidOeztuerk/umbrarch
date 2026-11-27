using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>Q-0221 (#157): readlink liest nur; eine Umleitung in einen Schutzpfad bleibt Deny.</summary>
public class Q0221ReadlinkTests
{
    private static PolicyContext RankA() =>
        new(ToolCallPolicyTests.Root, new ActiveQuest(new QuestId("Q-1"), Rank.A, new QuestScope(["src/**"], [])), false, new PolicyOptions());

    private static PolicyDecision Run(string cmd) => ToolCallPolicy.Evaluate(new ToolCall("Bash", Command: cmd), RankA());

    [Theory]
    [InlineData("readlink ~/.monarch/bin/monarch")]
    [InlineData("readlink -f ~/.monarch/bin/monarch")]
    [InlineData("readlink -n .monarch/ledger/a.jsonl")]
    [InlineData("/usr/bin/readlink ~/.monarch/bin/monarch")]
    public void Readlink_is_reading(string cmd)
    {
        var d = Run(cmd);
        Assert.True(d.Kind == PolicyDecisionKind.Allow, $"expected allow for: {cmd} (was {d.Kind}: {d.Reason})");
    }

    [Theory]
    [InlineData("readlink x > ~/.monarch/bin/monarch")]
    [InlineData("readlink x >> .monarch/ledger/a.jsonl")]
    [InlineData("readlink ~/.monarch/bin/monarch | tee .monarch/ledger/a.jsonl")]
    [InlineData("readlink x > .monarch/system.yaml")]
    public void Readlink_with_redirect_into_a_protected_path_is_denied(string cmd)
    {
        var d = Run(cmd);
        Assert.True(d.Kind == PolicyDecisionKind.Deny, $"expected deny for: {cmd} (was {d.Kind})");
        Assert.Equal(PolicyRules.ProtectPaths, d.Rule);
    }
}
