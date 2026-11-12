namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class GitFailureDescriberTests
{
    [TestCase("remote: Invalid username or token. Password authentication is not supported.\nfatal: Authentication failed for 'https://github.com/acme/app.git/'")]
    [TestCase("fatal: could not read Username for 'https://github.com': terminal prompts disabled")]
    [TestCase("fatal: unable to access 'https://github.com/acme/app.git/': The requested URL returned error: 403")]
    [TestCase("git@github.com: Permission denied (publickey).")]
    public void NoOrBadTokenIsExplainedAsAnAuthenticationFailure(string stderr)
        => Assert.That(GitFailureDescriber.Describe(stderr), Is.EqualTo("Authentication failed. Sign in to GitHub again and retry."));

    [Test]
    public void AnOrganizationThatEnforcesSingleSignOnAsksToAuthorizeTheTokenForSso()
    {
        const string stderr = "remote: The 'Acme' organization has enabled or enforced SAML SSO.\nremote: To access this repository, visit https://acme.ghe.com/enterprises/acme/sso?authorization_request=ABC123 and try your request again.\nfatal: unable to access 'https://acme.ghe.com/Acme/app.git/': The requested URL returned error: 403";

        var message = GitFailureDescriber.Describe(stderr);

        Assert.That(message, Does.StartWith("The Acme organization requires single sign-on (SAML SSO)"));
        Assert.That(message, Does.Contain("Configure SSO"));
        Assert.That(message, Does.EndWith("Or open https://acme.ghe.com/enterprises/acme/sso?authorization_request=ABC123"));
    }

    [Test]
    public void ASingleSignOnFailureWithoutDetailsStillNamesSso()
        => Assert.That(GitFailureDescriber.Describe("remote: SAML SSO is enforced.\nfatal: The requested URL returned error: 403"), Does.StartWith("This organization requires single sign-on (SAML SSO)").And.Not.Contain("Or open"));

    [TestCase(" ! [rejected]        HEAD -> feature (non-fast-forward)\nerror: failed to push some refs")]
    [TestCase(" ! [rejected]        HEAD -> feature (fetch first)")]
    public void ARemoteWithNewerCommitsIsExplainedAsARejectedPush(string stderr)
        => Assert.That(GitFailureDescriber.Describe(stderr), Does.StartWith("Push rejected"));

    [TestCase("fatal: No configured push destination.")]
    [TestCase("fatal: The current branch feature has no upstream branch.")]
    [TestCase("fatal: 'origin' does not appear to be a git repository\nfatal: Could not read from remote repository.")]
    [TestCase("fatal: No such remote: 'origin'")]
    public void AMissingRemoteOrUpstreamIsExplained(string stderr)
        => Assert.That(GitFailureDescriber.Describe(stderr), Is.EqualTo("No remote or upstream is configured for this branch."));

    [TestCase("fatal: unable to access 'https://github.com/acme/app.git/': Could not resolve host: github.com")]
    [TestCase("fatal: unable to access 'https://github.com/acme/app.git/': Failed to connect to github.com port 443 after 21000 ms: Couldn't connect to server")]
    [TestCase("ssh: connect to host github.com port 22: Network is unreachable")]
    public void AnUnreachableHostIsExplainedAsANetworkProblem(string stderr)
        => Assert.That(GitFailureDescriber.Describe(stderr), Does.StartWith("Could not reach the git host"));

    [Test]
    public void AnUnknownFailureGetsTheGenericMessage()
        => Assert.That(GitFailureDescriber.Describe("error: something nobody planned for"), Is.EqualTo("Git command failed."));

    [Test]
    public void AnEmptyOutputGetsTheGenericMessage()
        => Assert.That(GitFailureDescriber.Describe(string.Empty), Is.EqualTo("Git command failed."));

    [Test]
    public void RejectionWinsOverTheGenericAccessLine()
        => Assert.That(GitFailureDescriber.Describe("error: failed to push some refs to 'https://github.com/acme/app.git'\n ! [rejected] (fetch first)"), Does.StartWith("Push rejected"));

    [Test]
    public void AFailedRunKeepsTheRawOutputAsDetailsBehindTheFriendlyMessage()
    {
        var result = new GitResult(128, string.Empty, "fatal: could not read Username for 'https://github.com': terminal prompts disabled\n");

        var exception = GitProcessExtensions.Failure(result);

        Assert.That(exception.Message, Is.EqualTo("Authentication failed. Sign in to GitHub again and retry."));
        Assert.That(exception.Details, Is.EqualTo("fatal: could not read Username for 'https://github.com': terminal prompts disabled"));
        Assert.That(exception.ExitCode, Is.EqualTo(128));
    }

    [Test]
    public void AFailureThatOnlyWroteToStandardOutputStillHasDetails()
    {
        var result = new GitResult(1, "nothing to commit, working tree clean\n", string.Empty);

        var exception = GitProcessExtensions.Failure(result);

        Assert.That(exception.Message, Is.EqualTo("Git command failed."));
        Assert.That(exception.Details, Is.EqualTo("nothing to commit, working tree clean"));
    }
}
