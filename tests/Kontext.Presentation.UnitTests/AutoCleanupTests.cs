using Kontext.Domain;
using Kontext.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class AutoCleanupTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2025-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private FakeSessionClearer _clearer = null!;
    private FakeWorkspaceChanges _changes = null!;
    private FakeWorkspaceCommitter _committer = null!;
    private InMemoryAutoCleanupSetting _setting = null!;
    private AutoCleanup _cleanup = null!;

    [SetUp]
    public void CreateCleanup()
    {
        _clearer = new FakeSessionClearer();
        _changes = new FakeWorkspaceChanges();
        _committer = new FakeWorkspaceCommitter();
        _setting = new InMemoryAutoCleanupSetting();
        _cleanup = new AutoCleanup(_clearer, _changes, _committer, _setting);
    }

    private static IssueItemViewModel Item(int number, string state, IssueWorkStatus status = IssueWorkStatus.Ready, string? worktreePath = @"C:\wt\x")
        => new(Issues.Create(number, "Some work", "acme/memento") with { State = state }, new IssueWorkState(status, "kontext/x", worktreePath), Now);

    private List<int> ClearedNumbers() => _clearer.Cleared.Select(issue => issue.Number).ToList();

    [Test]
    public async Task ClearsAClosedIssueWhoseWorktreeHoldsNoWorkOfItsOwn()
    {
        await _cleanup.RunAsync([Item(7, "closed")], selected: null);

        Assert.That(ClearedNumbers(), Is.EqualTo(Expect.Seq(7)));
    }

    [Test]
    public async Task LeavesOpenIssuesAlone()
    {
        await _cleanup.RunAsync([Item(7, "open"), Item(8, "open", IssueWorkStatus.NeedsInput)], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }

    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.Preparing)]
    [TestCase(IssueWorkStatus.Failed)]
    public async Task LeavesAClosedIssueThatIsNotDoneAlone(IssueWorkStatus status)
    {
        await _cleanup.RunAsync([Item(7, "closed", status)], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }

    [Test]
    public async Task LeavesAClosedIssueWithoutAWorktreeAlone()
    {
        await _cleanup.RunAsync([Item(7, "closed", worktreePath: null)], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }

    [Test]
    public async Task LeavesTheIssueTheUserIsLookingAtAlone()
    {
        var shown = Item(7, "closed");

        await _cleanup.RunAsync([shown, Item(8, "closed")], selected: shown.Key);

        Assert.That(ClearedNumbers(), Is.EqualTo(Expect.Seq(8)));
    }

    [Test]
    public async Task LeavesAWorktreeWithUncommittedChangesAlone()
    {
        _changes.Changes = new WorkingTreeChanges([], [new FileChange("a.txt", null, FileChangeStatus.Modified, IsStaged: false, 1, 0, IsBinary: false)]);

        await _cleanup.RunAsync([Item(7, "closed")], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }

    [Test]
    public async Task LeavesAWorktreeWithUnpushedCommitsAlone()
    {
        _committer.Unpushed = 2;

        await _cleanup.RunAsync([Item(7, "closed")], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }

    [Test]
    public async Task DoesNothingWhileTurnedOff()
    {
        _setting.IsEnabled = false;

        await _cleanup.RunAsync([Item(7, "closed")], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }

    [Test]
    public async Task CarriesOnWithTheNextIssueWhenAClearFails()
    {
        _clearer.FailWith = new TestKontextException("worktree is locked");

        await _cleanup.RunAsync([Item(7, "closed"), Item(8, "closed")], selected: null);

        Assert.That(ClearedNumbers(), Is.EqualTo(Expect.Seq(7, 8)));
    }

    [Test]
    public async Task DoesNotClearTheSameIssueTwiceWhileAClearIsStillRunning()
    {
        _clearer.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = _cleanup.RunAsync([Item(7, "closed")], selected: null);

        await _cleanup.RunAsync([Item(7, "closed")], selected: null);
        _clearer.Gate.SetResult(true);
        await first;

        Assert.That(ClearedNumbers(), Is.EqualTo(Expect.Seq(7)));
    }

    [Test]
    public async Task ClearsAnIssueAgainOnceTheEarlierClearHasFinished()
    {
        _clearer.FailWith = new TestKontextException("worktree is locked");
        await _cleanup.RunAsync([Item(7, "closed")], selected: null);
        _clearer.FailWith = null;

        await _cleanup.RunAsync([Item(7, "closed")], selected: null);

        Assert.That(ClearedNumbers(), Is.EqualTo(Expect.Seq(7, 7)));
    }

    [Test]
    public async Task SkipsAWorktreeItCannotInspectAndCarriesOn()
    {
        _changes.FailWith = new TestKontextException("git failed");

        await _cleanup.RunAsync([Item(7, "closed"), Item(8, "closed")], selected: null);

        Assert.That(_clearer.Cleared, Is.Empty);
    }
}
