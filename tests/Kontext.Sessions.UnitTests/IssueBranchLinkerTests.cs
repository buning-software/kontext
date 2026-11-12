using Kontext.Testing;

namespace Kontext.Sessions.UnitTests;

[TestFixture]
public class IssueBranchLinkerTests
{
    private FakeIssueBranchLinks _links = null!;
    private FakeWorkspaceBranches _branches = null!;
    private IssueBranchLinker _linker = null!;

    [SetUp]
    public void CreateLinker()
    {
        _links = new FakeIssueBranchLinks();
        _branches = new FakeWorkspaceBranches();
        _linker = new IssueBranchLinker(_links, _branches);
    }

    [Test]
    public async Task LinksABranchGitHubDoesNotHaveYetFromWhereItStarts()
    {
        await _linker.LinkAsync(Issues.Create(7, "Some work", "acme/memento"), @"C:\wt\7");

        Assert.That(_links.Created, Is.EqualTo(Expect.Seq((7, "kontext/7-some-work", "base123"))));
    }

    [Test]
    public async Task LeavesABranchThatIsAlreadyLinkedAlone()
    {
        _links.Linked.Add("kontext/7-some-work");

        await _linker.LinkAsync(Issues.Create(7, "Some work", "acme/memento"), @"C:\wt\7");

        Assert.That(_links.Created, Is.Empty);
    }

    [Test]
    public async Task LeavesABranchThatWasPushedWithoutALinkAlone()
    {
        _branches.UnpublishedBase = null;

        await _linker.LinkAsync(Issues.Create(7, "Some work", "acme/memento"), @"C:\wt\7");

        Assert.That(_links.Created, Is.Empty);
    }

    [Test]
    public async Task AsksGitHubOnlyOnceForTheSameBranch()
    {
        var issue = Issues.Create(7, "Some work", "acme/memento");

        await _linker.LinkAsync(issue, @"C:\wt\7");
        await _linker.LinkAsync(issue, @"C:\wt\7");

        Assert.That(_links.Lookups, Is.EqualTo(1));
        Assert.That(_links.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TriesAgainAfterAFailure()
    {
        var issue = Issues.Create(7, "Some work", "acme/memento");
        _links.FailWith = new TestKontextException("offline");
        Assert.That(() => _linker.LinkAsync(issue, @"C:\wt\7"), Throws.InstanceOf<TestKontextException>());

        _links.FailWith = null;
        await _linker.LinkAsync(issue, @"C:\wt\7");

        Assert.That(_links.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LinksANewBranchOfTheSameIssue()
    {
        var issue = Issues.Create(7, "Some work", "acme/memento");
        await _linker.LinkAsync(issue, @"C:\wt\7");

        _branches.Branch = "kontext/7-second-try";
        await _linker.LinkAsync(issue, @"C:\wt\7");

        Assert.That(_links.Created.Select(created => created.Branch), Is.EqualTo(Expect.Seq("kontext/7-some-work", "kontext/7-second-try")));
    }
}
