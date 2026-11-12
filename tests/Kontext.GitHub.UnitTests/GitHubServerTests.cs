using Kontext.Domain;

namespace Kontext.GitHub.UnitTests;

[TestFixture]
public class GitHubServerTests
{
    [TestCase("github.corp.com", "github.corp.com")]
    [TestCase("  GitHub.Corp.com  ", "github.corp.com")]
    [TestCase("https://github.corp.com", "github.corp.com")]
    [TestCase("https://github.corp.com/", "github.corp.com")]
    [TestCase("github.corp.com:8443", "github.corp.com:8443")]
    [TestCase("acme.ghe.com", "acme.ghe.com")]
    [TestCase("https://maryor-software.ghe.com", "maryor-software.ghe.com")]
    public void AcceptsAServerAddressAndNormalisesItToAHostName(string address, string host)
    {
        var created = GitHubServer.TryCreate(address, out var server, out var error);

        Assert.That(created, Is.True, error);
        Assert.That(server!.Host, Is.EqualTo(host));
        Assert.That(server.IsEnterprise, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("localhost")]
    [TestCase("http://github.corp.com")]
    [TestCase("https://user@github.corp.com")]
    [TestCase("https://github.corp.com/some/path")]
    [TestCase("not a host")]
    public void RejectsAddressesThatCannotBeAServer(string? address)
    {
        Assert.That(GitHubServer.TryCreate(address, out var server, out var error), Is.False);
        Assert.That(server, Is.Null);
        Assert.That(error, Is.Not.Empty);
    }

    [TestCase("github.com")]
    [TestCase("https://github.com")]
    [TestCase("www.github.com")]
    public void SendsRegularGitHubBackToTheNormalSignIn(string address)
    {
        Assert.That(GitHubServer.TryCreate(address, out _, out var error), Is.False);
        Assert.That(error, Does.Contain("Sign in with GitHub"));
    }

    [Test]
    public void RegularGitHubIsNotAnEnterpriseServer()
    {
        Assert.That(GitHubServer.GitHub.IsEnterprise, Is.False);
        Assert.That(GitHubServer.GitHub.ApiUri, Is.EqualTo(new Uri("https://api.github.com/")));
    }

    [TestCase("github.corp.com", "https://github.corp.com/api/v3/")]
    [TestCase("github.corp.com:8443", "https://github.corp.com:8443/api/v3/")]
    [TestCase("acme.ghe.com", "https://api.acme.ghe.com/")]
    public void FindsTheApiOfAnEnterpriseServer(string host, string api)
    {
        Assert.That(new GitHubServer(host).ApiUri, Is.EqualTo(new Uri(api)));
    }

    [TestCase("github.com", "https://api.github.com/graphql")]
    [TestCase("github.corp.com", "https://github.corp.com/api/graphql")]
    [TestCase("github.corp.com:8443", "https://github.corp.com:8443/api/graphql")]
    [TestCase("acme.ghe.com", "https://api.acme.ghe.com/graphql")]
    public void FindsTheGraphQlEndpointOfEveryKindOfServer(string host, string endpoint)
    {
        Assert.That(new GitHubServer(host).GraphQlUri, Is.EqualTo(new Uri(endpoint)));
    }
}
