using Kontext.Domain;
using Kontext.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class GitChangesViewModelTests
{
    private const string Worktree = @"C:\wt\one";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private FakeWorkspaceChanges _fake = null!;
    private FakeWorkspaceCommitter _committer = null!;
    private FakeTimeProvider _clock = null!;
    private GitChangesViewModel _vm = null!;

    private ErrorBarViewModel Errors { get; set; } = null!;

    [SetUp]
    public void CreateViewModel()
    {
        _fake = new FakeWorkspaceChanges();
        _committer = new FakeWorkspaceCommitter();
        Errors = new ErrorBarViewModel();
        _clock = new FakeTimeProvider();
        _vm = new GitChangesViewModel(_fake, _committer, Errors, _clock, new GitChangesOptions(Interval));
    }

    [TearDown]
    public void DisposeViewModel() => _vm.Dispose();

    private static FileChange Change(string path, bool staged = false, int? additions = 1, int? deletions = 0)
        => new(path, null, FileChangeStatus.Modified, staged, additions, deletions, IsBinary: false);

    private static WorkingTreeChanges Tree(FileChange[] staged, FileChange[] unstaged) => new(staged, unstaged);

    private static WorkingTreeChanges Unstaged(params FileChange[] changes) => new([], changes);

    private void Activate(string path = Worktree)
    {
        _vm.WorktreePath = path;
        _vm.IsActive = true;
    }

    private static Task WaitFor(Func<bool> condition, string because) => AsyncAssert.EventuallyAsync(condition, because);

    private Task WaitForRequests(int count) => WaitFor(() => _fake.ChangeRequests.Count >= count, $"{count} request(s) for the changes");

    private async Task WaitForLoaded()
    {
        await WaitFor(() => _vm.HasLoaded, "the first load to finish");
        await WaitFor(() => _vm.SelectedChange is null || _vm.SelectedDiff is not null, "the first diff to load");
    }

    private static async Task Settle() => await Task.Delay(100);

    [Test]
    public async Task DoesNothingWhileInactive()
    {
        _vm.WorktreePath = Worktree;
        _clock.Advance(Interval * 3);
        await Settle();

        Assert.That(_fake.ChangeRequests, Is.Empty);
    }

    [Test]
    public async Task AsksForChangesAndDiffsOffTheCallingThread()
    {
        var caller = Environment.CurrentManagedThreadId;
        _fake.Changes = Unstaged(Change("a.cs"));

        Activate();
        await WaitForLoaded();

        Assert.That(_fake.ThreadIds, Has.Count.GreaterThanOrEqualTo(2), "one call for the changes and one for the diff");
        Assert.That(_fake.ThreadIds, Has.None.EqualTo(caller), "git and file work must not run on the thread that owns the UI");
    }

    [Test]
    public async Task DoesNothingWithoutAWorktree()
    {
        _vm.IsActive = true;
        _clock.Advance(Interval * 3);
        await Settle();

        Assert.That(_fake.ChangeRequests, Is.Empty);
    }

    [Test]
    public async Task LoadsImmediatelyWhenActivatedWithAWorktree()
    {
        _fake.Changes = Unstaged(Change("a.cs"));

        Activate();
        await WaitForLoaded();

        Assert.That(_fake.ChangeRequests, Is.EqualTo(Expect.Seq(Worktree)));
        Assert.That(_vm.Rows, Has.Count.EqualTo(2), "a header and the file");
    }

    [Test]
    public async Task PollsOnEveryIntervalWhileActive()
    {
        Activate();
        await WaitForRequests(1);

        _clock.Advance(Interval);
        await WaitForRequests(2);

        _clock.Advance(Interval);
        await WaitForRequests(3);
    }

    [Test]
    public async Task StopsPollingWhenDeactivated()
    {
        Activate();
        await WaitForRequests(1);

        _vm.IsActive = false;
        _clock.Advance(Interval);
        _clock.Advance(Interval);
        await Settle();

        Assert.That(_fake.ChangeRequests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadsAgainWhenReactivated()
    {
        Activate();
        await WaitForRequests(1);
        _vm.IsActive = false;

        _vm.IsActive = true;

        await WaitForRequests(2);
    }

    [Test]
    public async Task SettingTheSameValuesAgainDoesNothing()
    {
        Activate();
        await WaitForRequests(1);

        _vm.WorktreePath = Worktree;
        _vm.IsActive = true;
        _clock.Advance(Interval);
        await WaitForRequests(2);
        await Settle();

        Assert.That(_fake.ChangeRequests, Has.Count.EqualTo(2), "one immediate load and one poll, not a second loop");
    }

    [Test]
    public async Task ResetsAndReloadsWhenTheWorktreeChanges()
    {
        _fake.Changes = Unstaged(Change("old.cs"));
        Activate();
        await WaitForLoaded();

        _fake.Changes = Unstaged(Change("new.cs"));
        _vm.WorktreePath = @"C:\wt\two";

        await WaitFor(() => _vm.SelectedChange?.Path == "new.cs", "the other worktree's files");
        Assert.That(_fake.ChangeRequests, Is.EqualTo(Expect.Seq(Worktree, @"C:\wt\two")));
        Assert.That(_vm.Rows.Select(row => row.Text), Does.Not.Contain("old.cs"));
    }

    [Test]
    public async Task StopsAndClearsWhenTheWorktreeIsRemoved()
    {
        _fake.Changes = Unstaged(Change("a.cs"));
        Activate();
        await WaitForLoaded();

        _vm.WorktreePath = null;
        _clock.Advance(Interval);
        await Settle();

        Assert.That(_vm.Rows, Is.Empty);
        Assert.That(_vm.HasLoaded, Is.False);
        Assert.That(_vm.SelectedChange, Is.Null);
        Assert.That(_vm.SelectedDiff, Is.Null);
        Assert.That(_fake.ChangeRequests, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GroupsFilesUnderStagedAndChangesHeaders()
    {
        _fake.Changes = Tree([Change("x.cs", staged: true)], [Change("a.cs"), Change("b.cs"), Change("c.cs")]);

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.Rows.Select(row => row.Text), Is.EqualTo(Expect.Seq("Staged (1)", "x.cs", "Changes (3)", "a.cs", "b.cs", "c.cs")));
        Assert.That(_vm.Rows.Select(row => row.IsHeader), Is.EqualTo(Expect.Seq(true, false, true, false, false, false)));
    }

    [Test]
    public async Task OmitsAHeaderWhoseGroupIsEmpty()
    {
        _fake.Changes = Unstaged(Change("a.cs"));

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.Rows.Select(row => row.Text), Is.EqualTo(Expect.Seq("Changes (1)", "a.cs")));
    }

    [Test]
    public async Task SummarisesTheTotalsCountingAPartiallyStagedFileOnce()
    {
        _fake.Changes = Tree([Change("x.cs", staged: true, additions: 10, deletions: 2)], [Change("x.cs", additions: 1, deletions: 0), Change("y.cs", additions: 20, deletions: 7)]);

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.Summary, Is.EqualTo("2 files changed  +31  \u22129"));
    }

    [Test]
    public async Task SummarisesASingleFileInTheSingular()
    {
        _fake.Changes = Unstaged(Change("a.cs", additions: null, deletions: null));

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.Summary, Is.EqualTo("1 file changed  +0  \u22120"));
    }

    [Test]
    public async Task SelectsTheFirstFileAndLoadsItsDiff()
    {
        var staged = Change("s.cs", staged: true);
        var diff = new FileDiff("s.cs", [new DiffLine(DiffLineKind.Added, null, 1, "x")]);
        _fake.Changes = Tree([staged], [Change("u.cs")]);
        _fake.Diffs[("s.cs", true)] = diff;

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.SelectedChange, Is.EqualTo(staged));
        Assert.That(_vm.SelectedDiff, Is.EqualTo(diff));
        Assert.That(_vm.NeedsSelection, Is.False);
    }

    [Test]
    public async Task TellsWhetherAFileIsSelectedAndAnnouncesTheChange()
    {
        Assert.That(_vm.HasSelectedChange, Is.False);
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _fake.Changes = Unstaged(Change("a.cs"));

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.HasSelectedChange, Is.True);
        Assert.That(changed, Does.Contain(nameof(GitChangesViewModel.HasSelectedChange)));

        _vm.WorktreePath = null;
        Assert.That(_vm.HasSelectedChange, Is.False);
    }

    [Test]
    public async Task LoadsTheDiffWhenAnotherFileIsSelected()
    {
        var other = Change("u.cs");
        var diff = new FileDiff("u.cs", [new DiffLine(DiffLineKind.Added, null, 1, "y")]);
        _fake.Changes = Unstaged(Change("a.cs"), other);
        _fake.Diffs[("u.cs", false)] = diff;
        Activate();
        await WaitForLoaded();

        _vm.SelectedChange = other;

        await WaitFor(() => _vm.SelectedDiff == diff, "the diff of the newly selected file");
    }

    [Test]
    public async Task KeepsTheSelectionAcrossPollsEvenWhenItsCountsChange()
    {
        _fake.Changes = Unstaged(Change("a.cs"), Change("b.cs", additions: 1));
        Activate();
        await WaitForLoaded();
        _vm.SelectedChange = _vm.Rows[^1].Change;

        _fake.Changes = Unstaged(Change("a.cs"), Change("b.cs", additions: 5));
        _clock.Advance(Interval);

        await WaitFor(() => _vm.SelectedChange?.Additions == 5, "the selected file to pick up its new count");
        Assert.That(_vm.SelectedChange?.Path, Is.EqualTo("b.cs"));
    }

    [Test]
    public async Task SelectsTheFirstFileWhenTheSelectedOneDisappears()
    {
        _fake.Changes = Unstaged(Change("a.cs"), Change("b.cs"));
        Activate();
        await WaitForLoaded();
        _vm.SelectedChange = _vm.Rows[^1].Change;

        _fake.Changes = Unstaged(Change("a.cs"));
        _clock.Advance(Interval);

        await WaitFor(() => _vm.SelectedChange?.Path == "a.cs", "the first file to be selected");
    }

    [Test]
    public async Task DoesNotRaiseRowChangesWhenNothingChanged()
    {
        _fake.Changes = Unstaged(Change("a.cs"));
        Activate();
        await WaitForLoaded();
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _clock.Advance(Interval);
        await WaitForRequests(2);
        await Settle();

        Assert.That(changed, Does.Not.Contain(nameof(GitChangesViewModel.Rows)));
        Assert.That(changed, Does.Not.Contain(nameof(GitChangesViewModel.Summary)));
        Assert.That(changed, Does.Not.Contain(nameof(GitChangesViewModel.SelectedDiff)));
    }

    [Test]
    public async Task RefreshesTheSelectedDiffWhenItsContentChanges()
    {
        var first = new FileDiff("a.cs", [new DiffLine(DiffLineKind.Added, null, 1, "one")]);
        var second = new FileDiff("a.cs", [new DiffLine(DiffLineKind.Added, null, 1, "one"), new DiffLine(DiffLineKind.Added, null, 2, "two")]);
        _fake.Changes = Unstaged(Change("a.cs"));
        _fake.Diffs[("a.cs", false)] = first;
        Activate();
        await WaitFor(() => _vm.SelectedDiff == first, "the first diff");

        _fake.Diffs[("a.cs", false)] = second;
        _clock.Advance(Interval);

        await WaitFor(() => _vm.SelectedDiff == second, "the updated diff");
    }

    [Test]
    public async Task ShowsTheGitErrorAndKeepsTheLastData()
    {
        _fake.Changes = Unstaged(Change("a.cs"));
        Activate();
        await WaitForLoaded();

        _fake.FailWith = new InvalidOperationException("git exploded");
        _clock.Advance(Interval);

        await WaitFor(() => Errors.IsOpen, "the error to show");
        Assert.That(Errors.Message, Is.EqualTo("git exploded"));
        Assert.That(_vm.Rows, Has.Count.EqualTo(2), "the last good list stays");
        Assert.That(_vm.HasLoaded, Is.True);
        Assert.That(_vm.IsEmpty, Is.False);
    }

    [Test]
    public async Task ClearsTheErrorOnRecovery()
    {
        _fake.Changes = Unstaged(Change("a.cs"));
        Activate();
        await WaitForLoaded();
        _fake.FailWith = new InvalidOperationException("git exploded");
        _clock.Advance(Interval);
        await WaitFor(() => Errors.IsOpen, "the error to show");

        _fake.FailWith = null;
        _clock.Advance(Interval);

        await WaitFor(() => !Errors.IsOpen, "the error to clear");
        Assert.That(Errors.Message, Is.Null);
    }

    [Test]
    public async Task ShowsTheErrorEvenWhenTheFirstLoadFails()
    {
        _fake.FailWith = new InvalidOperationException("not a repository");

        Activate();

        await WaitFor(() => Errors.IsOpen, "the error to show");
        Assert.That(_vm.HasLoaded, Is.False);
        Assert.That(_vm.IsEmpty, Is.False);
    }

    [Test]
    public async Task ShowsTheEmptyStateForACleanTree()
    {
        Activate();

        await WaitFor(() => _vm.IsEmpty, "the empty state");
        Assert.That(_vm.HasChanges, Is.False);
        Assert.That(_vm.NeedsSelection, Is.False);
        Assert.That(_vm.Summary, Is.Empty);
    }

    [Test]
    public void ShowsTheUnifiedDiffUntilAskedForTheSplitView()
    {
        Assert.That(_vm.IsSplitView, Is.False);
        Assert.That(_vm.SplitRows, Is.Empty);
    }

    [Test]
    public void TellsWhenTheSplitViewIsSwitchedOnOrOff()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _vm.IsSplitView = true;

        Assert.That(changed, Does.Contain(nameof(GitChangesViewModel.IsSplitView)));
    }

    [Test]
    public async Task OffersTheSelectedDiffAsSideBySideRows()
    {
        var diff = new FileDiff("u.cs", [new DiffLine(DiffLineKind.Removed, 1, null, "old"), new DiffLine(DiffLineKind.Added, null, 1, "new")]);
        _fake.Changes = Unstaged(Change("u.cs"));
        _fake.Diffs[("u.cs", false)] = diff;

        Activate();
        await WaitForLoaded();

        Assert.That(_vm.SplitRows, Has.Count.EqualTo(1));
        Assert.That(_vm.SplitRows[0].Left?.Text, Is.EqualTo("old"));
        Assert.That(_vm.SplitRows[0].Right?.Text, Is.EqualTo("new"));
    }

    [Test]
    public async Task ClearsTheSideBySideRowsWhenTheSelectionIsLost()
    {
        _fake.Changes = Unstaged(Change("u.cs"));
        _fake.Diffs[("u.cs", false)] = new FileDiff("u.cs", [new DiffLine(DiffLineKind.Added, null, 1, "new")]);
        Activate();
        await WaitForLoaded();

        _vm.WorktreePath = @"C:\wt\two";

        Assert.That(_vm.SplitRows, Is.Empty);
    }

    [Test]
    public async Task IgnoresAStaleResultFromThePreviousWorktree()
    {
        _fake.Changes = Unstaged(Change("stale.cs"));
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _fake.Gate = gate;
        Activate();
        await WaitForRequests(1);

        _fake.Gate = null;
        _fake.Changes = Unstaged(Change("fresh.cs"));
        _vm.WorktreePath = @"C:\wt\two";
        await WaitFor(() => _vm.SelectedChange?.Path == "fresh.cs", "the new worktree's files");

        gate.SetResult(true);
        await Settle();

        Assert.That(_vm.Rows.Select(row => row.Text), Does.Not.Contain("stale.cs"));
        Assert.That(_vm.SelectedChange?.Path, Is.EqualTo("fresh.cs"));
    }

    private GitChangeRow FileRow(string path) => _vm.Rows.First(row => row.Change?.Path == path);

    private async Task LoadTwoFiles()
    {
        _fake.Changes = Unstaged(Change("a.cs"), Change("b.cs"));
        Activate();
        await WaitForLoaded();
    }

    [Test]
    public async Task CommitIsDisabledWithoutASelectionOrAMessage()
    {
        await LoadTwoFiles();
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.False);

        FileRow("a.cs").IsChecked = true;
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.False, "no message yet");

        _vm.CommitMessage = "   ";
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.False, "blank message");

        _vm.CommitMessage = "Fix a";
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.True);

        FileRow("a.cs").IsChecked = false;
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.False, "selection removed");
    }

    [Test]
    public async Task CommitsOnlyTheCheckedFilesAndResetsTheForm()
    {
        await LoadTwoFiles();
        FileRow("b.cs").IsChecked = true;
        _vm.CommitMessage = " Fix b ";
        _committer.OnCommitted = _ => _fake.Changes = Unstaged(Change("a.cs"));

        _vm.CommitCommand.Execute(null);
        await WaitFor(() => _vm.StatusMessage is not null, "the commit to finish");

        Assert.That(_committer.Commits, Has.Count.EqualTo(1));
        Assert.That(_committer.Commits[0].Changes.Select(change => change.Path).Single(), Is.EqualTo("b.cs"));
        Assert.That(_committer.Commits[0].Message, Is.EqualTo("Fix b"));
        Assert.That(_committer.Commits[0].WorktreePath, Is.EqualTo(Worktree));
        await WaitFor(() => _vm.Rows.All(row => row.Change?.Path != "b.cs"), "the list to refresh");
        Assert.That(_vm.CommitMessage, Is.Empty);
        Assert.That(_vm.CheckedCount, Is.Zero);
        Assert.That(_vm.UnpushedCount, Is.EqualTo(1));
        Assert.That(Errors.IsOpen, Is.False);
    }

    [Test]
    public async Task AFailedCommitIsShownAndKeepsTheMessageAndSelection()
    {
        await LoadTwoFiles();
        FileRow("a.cs").IsChecked = true;
        _vm.CommitMessage = "Fix a";
        _committer.CommitFailure = new InvalidOperationException("hook rejected the commit");

        _vm.CommitCommand.Execute(null);
        await WaitFor(() => Errors.IsOpen, "the error to show");

        Assert.That(Errors.Message, Is.EqualTo("hook rejected the commit"));
        Assert.That(_vm.CommitMessage, Is.EqualTo("Fix a"));
        Assert.That(_vm.CheckedCount, Is.EqualTo(1));
        Assert.That(FileRow("a.cs").IsChecked, Is.True);
        Assert.That(_vm.IsBusy, Is.False);
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task PushIsDisabledUntilThereIsSomethingToPush()
    {
        await LoadTwoFiles();
        Assert.That(_vm.PushCommand.CanExecute(null), Is.False);

        _committer.Unpushed = 2;
        _clock.Advance(Interval);
        await WaitFor(() => _vm.UnpushedCount == 2, "the unpushed count");

        Assert.That(_vm.PushCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task PushSendsTheWorktreeAndThenHasNothingLeftToPush()
    {
        _committer.Unpushed = 1;
        await LoadTwoFiles();
        await WaitFor(() => _vm.PushCommand.CanExecute(null), "push to be enabled");

        _vm.PushCommand.Execute(null);
        await WaitFor(() => _vm.StatusMessage is not null, "the push to finish");

        Assert.That(_committer.Pushes.Single(), Is.EqualTo(Worktree));
        await WaitFor(() => _vm.UnpushedCount == 0, "the unpushed count to refresh");
        Assert.That(_vm.PushCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task AFailedPushIsShownAndStaysPushable()
    {
        _committer.Unpushed = 1;
        _committer.PushFailure = new InvalidOperationException("Push rejected");
        await LoadTwoFiles();
        await WaitFor(() => _vm.PushCommand.CanExecute(null), "push to be enabled");

        _vm.PushCommand.Execute(null);
        await WaitFor(() => Errors.IsOpen && _vm.PushCommand.CanExecute(null), "the error to show and the push to finish");

        Assert.That(Errors.Message, Is.EqualTo("Push rejected"));
        Assert.That(_vm.UnpushedCount, Is.EqualTo(1));
        Assert.That(_vm.PushCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task CommitAndPushAreDisabledWhileAnActionRuns()
    {
        _committer.Unpushed = 1;
        await LoadTwoFiles();
        FileRow("a.cs").IsChecked = true;
        _vm.CommitMessage = "Fix a";
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _committer.Gate = gate;

        _vm.CommitCommand.Execute(null);
        await WaitFor(() => _vm.IsBusy, "the commit to start");

        Assert.That(_vm.BusyText, Is.Not.Empty);
        Assert.That(_vm.CommitCommand.CanExecute(null), Is.False);
        Assert.That(_vm.PushCommand.CanExecute(null), Is.False);

        gate.SetResult(true);
        await WaitFor(() => !_vm.IsBusy, "the commit to finish");
    }

    [Test]
    public async Task TheSelectionSurvivesAPollAndIsDroppedWhenTheFileDisappears()
    {
        await LoadTwoFiles();
        FileRow("a.cs").IsChecked = true;
        FileRow("b.cs").IsChecked = true;

        _fake.Changes = Unstaged(Change("a.cs"), Change("b.cs", additions: 5));
        _clock.Advance(Interval);
        await WaitFor(() => _vm.Rows.Any(row => row.Change?.Additions == 5), "the changed list");
        Assert.That(FileRow("a.cs").IsChecked, Is.True);
        Assert.That(FileRow("b.cs").IsChecked, Is.True);

        _fake.Changes = Unstaged(Change("a.cs"));
        _clock.Advance(Interval);
        await WaitFor(() => _vm.CheckedCount == 1, "b.cs to be dropped from the selection");
    }

    [Test]
    public async Task TheAllBoxChecksAndUnchecksEveryFile()
    {
        await LoadTwoFiles();
        Assert.That(_vm.AllChecked, Is.False);

        _vm.AllChecked = true;

        Assert.That(_vm.CheckedCount, Is.EqualTo(2));
        Assert.That(_vm.Rows.Where(row => !row.IsHeader).All(row => row.IsChecked), Is.True);
        Assert.That(_vm.AllChecked, Is.True);

        _vm.AllChecked = false;

        Assert.That(_vm.CheckedCount, Is.Zero);
        Assert.That(_vm.Rows.Where(row => !row.IsHeader).Any(row => row.IsChecked), Is.False);
    }

    [Test]
    public async Task TheAllBoxIsUndeterminedWhenOnlySomeFilesAreChecked()
    {
        await LoadTwoFiles();
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        FileRow("a.cs").IsChecked = true;

        Assert.That(_vm.AllChecked, Is.Null);
        Assert.That(changed, Does.Contain(nameof(GitChangesViewModel.AllChecked)));

        FileRow("b.cs").IsChecked = true;

        Assert.That(_vm.AllChecked, Is.True);
    }

    [Test]
    public async Task TheViewWritingTheUndeterminedStateBackKeepsTheSelection()
    {
        await LoadTwoFiles();
        FileRow("a.cs").IsChecked = true;

        _vm.AllChecked = null;

        Assert.That(_vm.CheckedCount, Is.EqualTo(1));
        Assert.That(FileRow("a.cs").IsChecked, Is.True);
    }

    [Test]
    public async Task AFileListedAsStagedAndChangedIsCheckedInBothRows()
    {
        _fake.Changes = Tree([Change("p.cs", staged: true)], [Change("p.cs")]);
        Activate();
        await WaitForLoaded();

        var rows = _vm.Rows.Where(row => row.Change?.Path == "p.cs").ToList();
        rows[0].IsChecked = true;

        Assert.That(rows[1].IsChecked, Is.True);
        Assert.That(_vm.CheckedCount, Is.EqualTo(1));
    }
}
