using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.GitAutomation.IntegrationTests;

[TestFixture]
public class EnterpriseCloneTests
{
    private static readonly RepositoryRef Corporate = new("acme", "app", Host: "github.corp.com");

    private TempDirectory _temp = null!;
    private GitAutomationOptions _options = null!;

    [SetUp]
    public void CreateScratchDirectory()
    {
        _temp = new TempDirectory();
        _options = new GitAutomationOptions(_temp.Combine("state"), _temp.Combine("worktrees"), _temp.Combine("repos"));
    }

    [TearDown]
    public void DeleteScratchDirectory() => _temp.Dispose();

    [Test]
    public async Task ClonesFromTheEnterpriseServerIntoAFolderNamedAfterIt()
    {
        var git = new FakeGitProcess();

        var target = await new RepositoryCloner(_options, git, new FolderRemover()).CloneAsync(Corporate);

        var expected = _temp.Combine("repos", "github.corp.com", "acme", "app");
        Assert.That(target, Is.EqualTo(expected));
        Assert.That(git.Runs.Single().Arguments, Is.EqualTo(Expect.Seq("clone", "https://github.corp.com/acme/app.git", expected)));
    }

    [Test]
    public void KeepsTheSameRepositoryNameOnTwoServersApart()
    {
        var store = new JsonRepositoryCloneStore(_options);
        store.Save(new RepositoryRef("acme", "app"), @"C:\clones\public");
        store.Save(Corporate, @"C:\clones\corporate");

        Assert.That(store.Find(new RepositoryRef("acme", "app")), Is.EqualTo(@"C:\clones\public"));
        Assert.That(store.Find(Corporate), Is.EqualTo(@"C:\clones\corporate"));
    }

    [Test]
    public void ListsRepositoriesWithTheirServer()
    {
        var store = new JsonRepositoryCloneStore(_options);
        store.Save(new RepositoryRef("acme", "app"), @"C:\clones\public");
        store.Save(Corporate, @"C:\clones\corporate");

        Assert.That(new JsonRepositoryCloneStore(_options).List(), Is.EquivalentTo(new[] { new RepositoryRef("acme", "app"), Corporate }));
    }
}
