namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class WorkspaceBranchesTests
{
    private ScriptedGitProcess _git = null!;
    private WorkspaceBranches _branches = null!;

    [SetUp]
    public void CreateService()
    {
        _git = new ScriptedGitProcess();
        _branches = new WorkspaceBranches(_git);
    }

    [Test]
    public async Task ReadsTheCheckedOutBranch()
    {
        _git.Answers["symbolic-ref"] = new GitResult(0, "features/29-upgrade\n", string.Empty);

        Assert.That(await _branches.GetBranchAsync(@"C:\wt"), Is.EqualTo("features/29-upgrade"));
        Assert.That(_git.Runs.Single(), Is.EqualTo(Expect.Seq("symbolic-ref", "--short", "HEAD")));
    }

    [Test]
    public void FailsWhenNoBranchIsCheckedOut()
    {
        _git.Answers["symbolic-ref"] = new GitResult(128, string.Empty, "fatal: ref HEAD is not a symbolic ref");

        Assert.That(() => _branches.GetBranchAsync(@"C:\wt"), Throws.InstanceOf<GitAutomationException>());
    }

    [Test]
    public async Task ReturnsWhereTheBranchLeftTheDefaultBranchWhileOriginDoesNotHaveIt()
    {
        _git.Answers["merge-base"] = new GitResult(0, "abc123\n", string.Empty);

        var found = await _branches.FindUnpublishedBaseAsync(@"C:\wt", "features/29-upgrade");

        Assert.That(found, Is.EqualTo("abc123"));
        Assert.That(_git.Runs[0], Is.EqualTo(Expect.Seq("ls-remote", "--heads", "origin", "refs/heads/features/29-upgrade")));
        Assert.That(_git.Runs[1], Is.EqualTo(Expect.Seq("merge-base", "HEAD", "refs/remotes/origin/HEAD")));
    }

    [Test]
    public async Task ReturnsNothingOnceOriginHasTheBranch()
    {
        _git.Answers["ls-remote"] = new GitResult(0, "abc123\trefs/heads/features/29-upgrade\n", string.Empty);

        Assert.That(await _branches.FindUnpublishedBaseAsync(@"C:\wt", "features/29-upgrade"), Is.Null);
        Assert.That(_git.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReturnsNothingWithoutADefaultBranchToStartFrom()
    {
        _git.Answers["merge-base"] = new GitResult(128, string.Empty, "fatal: Not a valid object name refs/remotes/origin/HEAD");

        Assert.That(await _branches.FindUnpublishedBaseAsync(@"C:\wt", "features/29-upgrade"), Is.Null);
    }

    [Test]
    public async Task ReturnsNothingWhenGitFindsNoSharedCommit()
    {
        _git.Answers["merge-base"] = new GitResult(0, "\n", string.Empty);

        Assert.That(await _branches.FindUnpublishedBaseAsync(@"C:\wt", "features/29-upgrade"), Is.Null);
    }

    [TestCase("")]
    [TestCase("  ")]
    public void RefusesABlankWorktreeOrBranchWithoutRunningGit(string blank)
    {
        Assert.That(() => _branches.GetBranchAsync(blank), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => _branches.FindUnpublishedBaseAsync(blank, "features/29-upgrade"), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => _branches.FindUnpublishedBaseAsync(@"C:\wt", blank), Throws.InstanceOf<ArgumentException>());
        Assert.That(_git.Runs, Is.Empty);
    }

    [Test]
    public void FailsWhenOriginCannotBeReached()
    {
        _git.Answers["ls-remote"] = new GitResult(128, string.Empty, "fatal: unable to access 'https://github.com/acme/memento.git/'");

        Assert.That(() => _branches.FindUnpublishedBaseAsync(@"C:\wt", "features/29-upgrade"), Throws.InstanceOf<GitAutomationException>());
    }
}
