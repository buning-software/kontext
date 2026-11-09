using Kontext.Kernel;
using Kontext.Testing;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class EnterpriseHostTests
{
    private static readonly RepositoryRef Corporate = new("acme", "app", Host: "github.corp.com");

    [TestCase("https://github.corp.com/acme/app.git")]
    [TestCase("git@github.corp.com:acme/app.git")]
    [TestCase("ssh://git@github.corp.com/acme/app.git")]
    public void RecognisesRemotesOfTheGivenHost(string url)
    {
        var repository = GitRemoteUrlParser.Parse(url, "github.corp.com");

        Assert.That(repository, Is.EqualTo(Corporate));
    }

    [Test]
    public void ParsesAHostWithAPort()
    {
        var repository = GitRemoteUrlParser.Parse("https://github.corp.com:8443/acme/app.git", "github.corp.com:8443");

        Assert.That(repository?.FullName, Is.EqualTo("acme/app"));
    }

    [TestCase("https://github.com/acme/app.git")]
    [TestCase("https://github.corp.com.evil.example/acme/app.git")]
    public void IgnoresRemotesOfOtherHosts(string url)
    {
        Assert.That(GitRemoteUrlParser.Parse(url, "github.corp.com"), Is.Null);
    }

    [Test]
    public void DoesNotTreatARemoteOfTheEnterpriseServerAsRegularGitHub()
    {
        Assert.That(GitRemoteUrlParser.Parse("https://github.corp.com/acme/app.git"), Is.Null);
    }

    [Test]
    public void KeepsEnterpriseWorktreesApartFromRegularGitHubOnes()
    {
        var provider = new WorktreePathProvider(new GitAutomationOptions(@"C:\state", @"C:\worktrees", @"C:\repos"));

        var path = provider.GetPath(Corporate, "features/7-fix");

        Assert.That(path, Is.EqualTo(Path.Combine(@"C:\worktrees", "github.corp.com", "acme", "app", "7-fix")));
    }

    [Test]
    public void MakesAHostWithAPortSafeAsAFolderName()
    {
        var provider = new WorktreePathProvider(new GitAutomationOptions(@"C:\state", @"C:\worktrees", @"C:\repos"));

        var path = provider.GetPath(new RepositoryRef("acme", "app", Host: "github.corp.com:8443"), "features/7-fix");

        Assert.That(path, Is.EqualTo(Path.Combine(@"C:\worktrees", "github.corp.com_8443", "acme", "app", "7-fix")));
    }

    [Test]
    public async Task OnlyAcceptsACloneOfTheSameHost()
    {
        var onGitHub = new RepositoryRef("acme", "app");
        var inspector = new FakeInspector().WithRepository(@"C:\clones\app", @"C:\clones\app", onGitHub);
        var resolver = new RepositoryCloneResolver(new FakeCloneStore(), new FakeCloner(@"C:\clones\app"), inspector);

        var ex = await Assert.ThrowsAsync<GitAutomationException>(() => resolver.ResolveAsync(Corporate));

        Assert.That(ex.Message, Does.Contain("github.corp.com"));
    }

    [Test]
    public async Task AcceptsACloneWhoseRemoteIsTheEnterpriseServer()
    {
        var inspector = new FakeInspector().WithRepository(@"C:\clones\app", @"C:\clones\app", Corporate);
        var resolver = new RepositoryCloneResolver(new FakeCloneStore(), new FakeCloner(@"C:\clones\app"), inspector);

        var resolved = await resolver.ResolveAsync(Corporate);

        Assert.That(resolved, Is.EqualTo(Corporate with { LocalPath = @"C:\clones\app" }));
    }
}
