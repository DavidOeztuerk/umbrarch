using Monarch.Domain;
using Monarch.Policy;

namespace Monarch.Policy.Tests;

/// <summary>
/// Q-0220 (ADR-0022 §5): Wächter, die Deny bleiben, und die Vorlagen-Ausnahmen. Eine Lockerung für Suchmuster und jq-Filter (#66, #169) gibt es nicht:
/// auf Hook-Ebene ist sie mit Text-Parsing nicht sicher zu machen.
/// </summary>
public class Q0220SecretPatternTests
{
    private const string Home = "/home/u";

    private static PolicyContext Ctx() => ToolCallPolicyTests.Ctx() with { HomeDir = Home };

    private static PolicyDecision Run(ToolCall c) => ToolCallPolicy.Evaluate(c, Ctx());

    private static void Allowed(ToolCall c) => Assert.Equal(PolicyDecisionKind.Allow, Run(c).Kind);

    private static void DeniedBySecrets(ToolCall c)
    {
        var d = Run(c);
        Assert.Equal(PolicyDecisionKind.Deny, d.Kind);
        Assert.Equal(PolicyRules.ProtectSecrets, d.Rule);
    }

    // Umgehungen aus den Warden-Runden zu Q-0220: Quotes, Wrapper, Zuweisungen, Verschachtelung, Backtick-Ende, Options-Variablen
    [Theory]
    [InlineData("`grep '`;cat .env;`'`")]
    [InlineData("`grep -e '`;cat .env;`'` x")]
    [InlineData("export GREP_OPTIONS='-e x'; grep '.env'")]
    [InlineData("export RIPGREP_CONFIG_PATH=/tmp/c; rg '.env'")]
    [InlineData("grep -e x -e id_rsa ci.yml")]
    [InlineData("grep --regexp='\\.env' ci.yml")]
    [InlineData("grep \".env\" y")]
    [InlineData("grep \"a|.env(x)\" y")]
    [InlineData("grep a=.env y")]
    [InlineData("grep '.env'/.env y")]
    [InlineData("grep x' '.env y")]
    [InlineData("grep '.e''nv' y")]
    [InlineData("grep $'.env' y")]
    [InlineData("sudo grep '\\.env' x")]
    [InlineData("env X=1 grep '\\.env' x")]
    [InlineData("X=1 grep '\\.env' x")]
    [InlineData("command grep '\\.env' x")]
    [InlineData("nohup grep '\\.env' x")]
    [InlineData("if grep '\\.env' x; then true; fi")]
    [InlineData("bash -c \"grep '\\.env' x\"")]
    [InlineData("eval grep \"'.env'\" x")]
    [InlineData("jq -n --arg a b .env")]
    public void Bypasses_from_warden_rounds_stay_denied(string cmd) => DeniedBySecrets(ToolCallPolicyTests.Bash(cmd));

    // Warden G-0132 (Fix C): sudo -s/-i übergibt an eine Shell, die $IFS erneut teilt
    [Theory]
    [InlineData("sudo -s grep 'x$IFS./.env'")]
    [InlineData("sudo -i grep 'x$IFS./.env'")]
    [InlineData("sudo --shell grep 'x$IFS./.env'")]
    [InlineData("sudo --login grep 'x$IFS./.env'")]
    [InlineData("sudo -sE grep 'x$IFS./.env'")]
    [InlineData("sudo -u root -s grep 'x$IFS./.env'")]
    [InlineData("sudo -s grep 'x$IFS,.env'")]
    [InlineData("sudo -s jq 'x$IFS./.env'")]
    [InlineData("sudo -s rg 'x$IFS./.env'")]
    [InlineData("sudo grep 'x$IFS./.env'")]
    public void Sudo_shell_modes_are_strict(string cmd) => DeniedBySecrets(ToolCallPolicyTests.Bash(cmd));

    // Ursache #169: nicht der Pfad unter ~/.claude/plugins, sondern ".key" im Filter trifft **/*.key.
    [Fact]
    public void Cause_169_is_the_key_token_in_the_filter_not_the_plugin_path()
    {
        Allowed(ToolCallPolicyTests.Bash("jq . ~/.claude/plugins/installed_plugins.json"));
        Allowed(ToolCallPolicyTests.Bash("jq '.\"umbrarch-local\", .\"monarch-local\"' ~/.claude/plugins/known_marketplaces.json"));
        Assert.Contains(".key", BashAnalysis.PathTokens("'.plugins | with_entries(select(.key|test(\"umbrarch|monarch\")))'"));
        Assert.Matches(@"^\*\*/\*\.key$", PolicyOptions.DefaultSecretGlobs.Single(g => g.EndsWith(".key", StringComparison.Ordinal)));
    }

    // Vorlagen (Owner-Antwort zu Probe i): .env.sample und .env.template sind lesbar wie .env.example
    [Theory]
    [InlineData(".env.sample"), InlineData("app/.env.template"), InlineData("/home/u/p/.env.sample")]
    public void Templates_are_readable(string path)
    {
        Allowed(new ToolCall("Read", FilePath: path));
        Allowed(ToolCallPolicyTests.Bash("cat " + path));
        Allowed(new ToolCall("Grep", FilePath: path));
    }

    // Wächter aus ADR-0022 §5 und weitere: Dateioperanden bleiben Deny
    [Theory]
    [InlineData("grep foo .env")]
    [InlineData("grep -f .env x")]
    [InlineData("grep foo x .env")]
    [InlineData("grep -e x .env")]
    [InlineData("grep --regexp=x .env")]
    [InlineData("grep --file=.env x")]
    [InlineData("grep -ef .env x")]
    [InlineData("rg -f .env x")]
    [InlineData("rg -g .env x")]
    [InlineData("jq . .env")]
    [InlineData("jq -f p.jq .env")]
    [InlineData("jq --from-file=.env")]
    [InlineData("jq --rawfile a .env -n 1")]
    [InlineData("jq --slurpfile a .env -n 1")]
    [InlineData("cat ~/.netrc")]
    [InlineData("grep -- .env x")]
    [InlineData("grep -A 1 .env x")]
    [InlineData("grep -e x -- .env")]
    [InlineData("rg --glob=.env x")]
    [InlineData("rg --iglob .env x")]
    [InlineData("jq --arg a .env . x")]
    [InlineData("jq --argjson a .env . x")]
    [InlineData("jq -n --args . .env")]
    [InlineData("jq .key a.key")]
    [InlineData("grep x < .env")]
    [InlineData("grep x .env.local")]
    [InlineData("grep x .env.vor-umbenennung")]
    [InlineData("rg x ~/.netrc")]
    [InlineData("grep -rn x .env")]
    [InlineData("cat .env | grep x")]
    [InlineData("grep x .env.sample.key")]
    [InlineData("grep .env -e x y")]
    [InlineData("grep server.key -f p.txt y")]
    [InlineData("jq .env -f p.jq")]
    [InlineData("bash -c 'grep foo .env'")]
    [InlineData("POSIXLY_CORRECT=1 grep foo -e .env")]
    [InlineData("POSIXLY_CORRECT=1 grep foo -e .env.vor-umbenennung")]
    [InlineData("env POSIXLY_CORRECT=1 grep foo -e .env")]
    [InlineData("grep -- -e .env")]
    [InlineData("grep foo -- -e .env")]
    [InlineData("grep -e x y -e .env")]
    [InlineData("rg foo -e .env")]
    [InlineData("jq . x.json --arg a .env")]
    [InlineData("grep foo${IFS}.env x")]
    [InlineData("grep ${IFS}.env x")]
    [InlineData("rg foo${IFS}.env x")]
    [InlineData("jq .${IFS}.env")]
    [InlineData("jq -n .${IFS}.env")]
    [InlineData("grep -e foo${IFS}.env x")]
    [InlineData("grep .env.* x")]
    [InlineData("grep id_rsa* x")]
    [InlineData("grep *.key x")]
    [InlineData("grep ~/.ssh/id_rsa* x")]
    [InlineData("grep --regexp=foo${IFS}.env x")]
    [InlineData("grep .env(|.local) y")]
    [InlineData("grep -e .env(|.local) y")]
    [InlineData("grep --regexp=.env(|.local) y")]
    [InlineData("jq .env(|.local)")]
    [InlineData("grep .env(|.vor-umbenennung) y")]
    [InlineData("grep \"$(cat .env)\" x")]
    [InlineData("grep \"`cat .env`\" x")]
    [InlineData("grep \"\\$x\" .env")]
    [InlineData("grep =.env x")]
    [InlineData("jq \"$(cat .env)\"")]
    [InlineData("grep \"\" .env")]
    [InlineData("grep '' .env")]
    [InlineData("grep $(echo foo) .env")]
    [InlineData("grep \"$(echo foo)\" .env")]
    [InlineData("grep .env<-> y")]
    [InlineData("grep ~/.env y")]
    [InlineData("grep \"$X\" .env")]
    [InlineData("grep $'\\x2e'env y")]
    [InlineData("eval grep .env(|.local)")]
    [InlineData("command eval grep .env(|.local)")]
    [InlineData("eval grep -e .env(|.local) x")]
    [InlineData("eval jq .env(|.local)")]
    [InlineData("eval rg .env(|.local)")]
    [InlineData("eval grep id_rsa(|.pub)")]
    [InlineData("eval grep .env<-> x")]
    [InlineData("eval grep .env(N) .env")]
    [InlineData("eval grep \"$(echo foo)\" .env")]
    [InlineData("eval grep `echo foo` .env")]
    [InlineData("bash -c 'eval grep .env(|.local)'")]
    [InlineData("bash -c \"grep $(echo foo) .env\"")]
    [InlineData("bash -c \"grep `echo foo` .env\"")]
    [InlineData("zsh -c \"grep $(echo foo) .env\"")]
    [InlineData("sh -c \"jq $(echo .) .env\"")]
    public void Guards_stay_denied(string cmd) => DeniedBySecrets(ToolCallPolicyTests.Bash(cmd));

    // Gängige Optionen ohne Wert: die Datei hinter dem Muster bleibt geprüft.
    [Theory]
    [InlineData("grep", "-n"), InlineData("grep", "-i"), InlineData("grep", "-E"), InlineData("grep", "-F"), InlineData("grep", "-G")]
    [InlineData("grep", "-P"), InlineData("grep", "-w"), InlineData("grep", "-x"), InlineData("grep", "-c"), InlineData("grep", "-l")]
    [InlineData("grep", "-L"), InlineData("grep", "-o"), InlineData("grep", "-q"), InlineData("grep", "-s"), InlineData("grep", "-v")]
    [InlineData("grep", "-H"), InlineData("grep", "-h"), InlineData("grep", "--line-number"), InlineData("grep", "--ignore-case")]
    [InlineData("grep", "--extended-regexp"), InlineData("grep", "--fixed-strings"), InlineData("grep", "--basic-regexp")]
    [InlineData("grep", "--perl-regexp"), InlineData("grep", "--word-regexp"), InlineData("grep", "--line-regexp"), InlineData("grep", "--count")]
    [InlineData("grep", "--files-with-matches"), InlineData("grep", "--files-without-match"), InlineData("grep", "--only-matching")]
    [InlineData("grep", "--quiet"), InlineData("grep", "--silent"), InlineData("grep", "--no-messages"), InlineData("grep", "--invert-match")]
    [InlineData("grep", "--with-filename"), InlineData("grep", "--no-filename")]
    [InlineData("rg", "-n"), InlineData("rg", "-i"), InlineData("rg", "-S"), InlineData("rg", "-s"), InlineData("rg", "-F"), InlineData("rg", "-w")]
    [InlineData("rg", "-x"), InlineData("rg", "-c"), InlineData("rg", "-l"), InlineData("rg", "-o"), InlineData("rg", "-q"), InlineData("rg", "-v")]
    [InlineData("rg", "-N"), InlineData("rg", "--line-number"), InlineData("rg", "--ignore-case"), InlineData("rg", "--smart-case")]
    [InlineData("rg", "--case-sensitive"), InlineData("rg", "--fixed-strings"), InlineData("rg", "--word-regexp"), InlineData("rg", "--line-regexp")]
    [InlineData("rg", "--count"), InlineData("rg", "--files-with-matches"), InlineData("rg", "--only-matching"), InlineData("rg", "--quiet")]
    [InlineData("rg", "--invert-match"), InlineData("rg", "--no-line-number")]
    [InlineData("jq", "-r"), InlineData("jq", "-c"), InlineData("jq", "-n"), InlineData("jq", "-e"), InlineData("jq", "-s"), InlineData("jq", "-j")]
    [InlineData("jq", "-a"), InlineData("jq", "-S"), InlineData("jq", "-C"), InlineData("jq", "-M"), InlineData("jq", "--raw-output")]
    [InlineData("jq", "--compact-output"), InlineData("jq", "--null-input"), InlineData("jq", "--exit-status"), InlineData("jq", "--slurp")]
    [InlineData("jq", "--join-output"), InlineData("jq", "--ascii-output"), InlineData("jq", "--sort-keys"), InlineData("jq", "--color-output")]
    [InlineData("jq", "--monochrome-output"), InlineData("jq", "--tab")]
    public void Listed_flags_take_no_value(string word, string flag)
    {
        DeniedBySecrets(ToolCallPolicyTests.Bash($"{word} {flag} x .env"));
        DeniedBySecrets(ToolCallPolicyTests.Bash($"{word} {flag} x .env.vor-umbenennung"));
    }
}
