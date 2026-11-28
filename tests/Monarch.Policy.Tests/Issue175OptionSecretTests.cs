using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>
/// #175 (ADR-0022, Restrisiko): Werte direkt an einer Option (-f.env, --include=.env.x) werden wie Pfad-Token geprüft.
/// </summary>
public class Issue175OptionSecretTests
{
    private const string Home = "/home/u";

    private static PolicyContext Ctx() => ToolCallPolicyTests.Ctx() with { HomeDir = Home };

    private static PolicyDecision Run(ToolCall c) => ToolCallPolicy.Evaluate(c, Ctx());

    private static void DeniedBySecrets(ToolCall c)
    {
        var d = Run(c);
        Assert.Equal(PolicyDecisionKind.Deny, d.Kind);
        Assert.Equal(PolicyRules.ProtectSecrets, d.Rule);
    }

    private static void Allowed(ToolCall c) => Assert.Equal(PolicyDecisionKind.Allow, Run(c).Kind);

    [Theory]
    [InlineData("grep -f.env x")]
    [InlineData("rg -f.env x")]
    [InlineData("grep -r --include=.env.vor-umbenennung PASS .")]
    [InlineData("grep --file=.env x")]
    [InlineData("grep -nf.env x")]
    [InlineData("rg -nf.env x")]
    [InlineData("grep -rf.env x")]
    [InlineData("grep -f'.env' x")]
    [InlineData("grep -n -f.env x")]
    [InlineData("true && grep -f.env x")]
    public void Option_attached_secret_values_are_denied(string cmd) => DeniedBySecrets(ToolCallPolicyTests.Bash(cmd));

    [Theory]
    [InlineData("grep -n -fheader.txt x")]
    [InlineData("grep -r --include=*.ts TODO .")]
    [InlineData("rg -n --glob=*.cs Foo src")]
    public void Option_values_without_secret_names_stay_allowed(string cmd) => Allowed(ToolCallPolicyTests.Bash(cmd));
}
