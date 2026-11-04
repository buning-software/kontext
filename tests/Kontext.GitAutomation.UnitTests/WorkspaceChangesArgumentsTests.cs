using Kontext.Domain;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class WorkspaceChangesArgumentsTests
{
    // One file that is both staged and modified again, so every git command is needed.
    private const string BothStagedAndUnstaged = "1 MM N... 100644 100644 100644 aaa bbb a.cs\0";

    private FakeGitProcess _git = null!;
    private WorkspaceChanges _changes = null!;

    [SetUp]
    public void CreateService()
    {
        _git = new FakeGitProcess();
        _changes = new WorkspaceChanges(_git);
    }

    [Test]
    public async Task EveryGitCallStartsWithNoOptionalLocks()
    {
        _git.Result = new GitResult(0, BothStagedAndUnstaged, string.Empty);

        await _changes.GetChangesAsync(@"C:\wt");
        await _changes.GetDiffAsync(@"C:\wt", new FileChange("a.cs", null, FileChangeStatus.Modified, IsStaged: true, 1, 1, IsBinary: false));
        await _changes.GetDiffAsync(@"C:\wt", new FileChange("a.cs", null, FileChangeStatus.Modified, IsStaged: false, 1, 1, IsBinary: false));

        Assert.That(_git.Runs, Has.Count.EqualTo(5), "status, two numstat runs and two diffs");
        Assert.That(_git.Runs.Select(run => run.Arguments[0]), Is.All.EqualTo("--no-optional-locks"));
        Assert.That(_git.Runs.Select(run => run.WorkingDirectory), Is.All.EqualTo(@"C:\wt"));
    }

    [Test]
    public async Task AskForStatusWithNulSeparatedPorcelainV2AndEveryUntrackedFile()
    {
        await _changes.GetChangesAsync(@"C:\wt");

        Assert.That(_git.Runs.Single().Arguments, Is.EqualTo(Expect.Seq("--no-optional-locks", "status", "--porcelain=v2", "-z", "--untracked-files=all")));
    }

    [Test]
    public async Task DoesNotCountLinesWhenNothingChanged()
    {
        var changes = await _changes.GetChangesAsync(@"C:\wt");

        Assert.That(changes, Is.EqualTo(WorkingTreeChanges.Empty));
        Assert.That(_git.Runs, Has.Count.EqualTo(1), "only the status call is needed for a clean tree");
    }

    [Test]
    public async Task ThePathIsPassedAfterTheSeparatorAsOneArgument()
    {
        await _changes.GetDiffAsync(@"C:\wt", new FileChange("-weird file.txt", null, FileChangeStatus.Modified, IsStaged: false, 1, 0, IsBinary: false));

        var arguments = _git.Runs.Single().Arguments;
        Assert.That(arguments[^2..], Is.EqualTo(Expect.Seq("--", "-weird file.txt")));
        Assert.That(arguments, Does.Not.Contain("--cached"));
    }

    [Test]
    public async Task DoesNotRunGitForADiffTooLargeToPreview()
    {
        var diff = await _changes.GetDiffAsync(@"C:\wt", new FileChange("package-lock.json", null, FileChangeStatus.Modified, IsStaged: false, 15_000, 10_000, IsBinary: false));

        Assert.That(_git.Runs, Is.Empty, "reading a huge diff into memory every poll would stall the app");
        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("Diff too large to preview."));
    }

    [Test]
    public async Task StillShowsADiffJustBelowTheLimit()
    {
        await _changes.GetDiffAsync(@"C:\wt", new FileChange("big.cs", null, FileChangeStatus.Modified, IsStaged: false, 10_000, 9_999, IsBinary: false));

        Assert.That(_git.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AStagedDiffUsesCachedAndBothPathsOfARename()
    {
        await _changes.GetDiffAsync(@"C:\wt", new FileChange("new.txt", "old.txt", FileChangeStatus.Renamed, IsStaged: true, 0, 0, IsBinary: false));

        var arguments = _git.Runs.Single().Arguments;
        Assert.That(arguments, Does.Contain("--cached"));
        Assert.That(arguments[^3..], Is.EqualTo(Expect.Seq("--", "old.txt", "new.txt")));
    }
}
