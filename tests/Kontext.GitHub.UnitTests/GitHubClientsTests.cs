using Kontext.Kernel;
using Octokit;

namespace Kontext.GitHub.UnitTests;

[TestFixture]
public class GitHubClientsTests
{
    private GitHubClients _clients = null!;

    [SetUp]
    public void CreateClients() => _clients = new GitHubClients();

    private static GitHubClient Client(string token) => new(new ProductHeaderValue("Kontext.Tests")) { Credentials = new Credentials(token) };

    [Test]
    public void GivesTheClientOfASignedInHost()
    {
        var client = Client("token-a");
        _clients.Set(GitHubHosts.Default, client);

        Assert.That(_clients.For(GitHubHosts.Default), Is.SameAs(client));
    }

    [Test]
    public void TreatsHostNamesWithoutRegardToCase()
    {
        var client = Client("token-a");
        _clients.Set("acme.ghe.com", client);

        Assert.That(_clients.For("ACME.ghe.com"), Is.SameAs(client));
    }

    [Test]
    public void GivesTheTokenOfTheHostItIsAskedFor()
    {
        _clients.Set(GitHubHosts.Default, Client("token-a"));
        _clients.Set("acme.ghe.com", Client("token-b"));

        Assert.That(_clients.TokenFor("acme.ghe.com"), Is.EqualTo("token-b"));
    }

    [Test]
    public void ReportsAHostThatIsNotSignedIn()
    {
        Assert.That(() => _clients.For("acme.ghe.com"), Throws.InstanceOf<GitHubException>().With.Message.EqualTo("You are not signed in to acme.ghe.com."));
        Assert.That(() => _clients.TokenFor("acme.ghe.com"), Throws.InstanceOf<GitHubException>());
    }

    [Test]
    public void ForgetsAHostThatSignedOut()
    {
        _clients.Set("acme.ghe.com", Client("token-b"));

        _clients.Remove("acme.ghe.com");

        Assert.That(() => _clients.For("acme.ghe.com"), Throws.InstanceOf<GitHubException>());
        Assert.That(_clients.All(), Is.Empty);
    }

    [Test]
    public void ASecondSignInReplacesTheFirst()
    {
        _clients.Set(GitHubHosts.Default, Client("token-a"));
        _clients.Set(GitHubHosts.Default, Client("token-c"));

        Assert.That(_clients.All(), Has.Length.EqualTo(1));
        Assert.That(_clients.TokenFor(GitHubHosts.Default), Is.EqualTo("token-c"));
    }
}
