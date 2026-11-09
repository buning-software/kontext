using Kontext.Domain;
using Kontext.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class IssueSidebarAutoRefreshTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private FakeTimeProvider _clock = null!;
    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeGitHubService _gitHub = null!;
    private FakeExistingWorkspaceFinder _workspaces = null!;
    private IssueSidebarViewModel? _sidebar;

    [SetUp]
    public void Setup()
    {
        _clock = new FakeTimeProvider();
        _tracker = new InMemoryIssueWorkTracker();
        _gitHub = new FakeGitHubService { Issues = [Issues.Create(4, "Add notifications", "acme/memento")] };
        _workspaces = new FakeExistingWorkspaceFinder();
    }

    [TearDown]
    public void Teardown() => _sidebar?.Dispose();

    private IssueSidebarViewModel CreateSidebar(TimeSpan interval) =>
        _sidebar = new(
            _gitHub,
            _tracker,
            _workspaces,
            new WorkspaceIssues(_gitHub),
            new InMemoryHiddenRepositoryStore(),
            new InMemoryCollapsedRepositoryStore(),
            new FakeSessionStore(),
            new FakeTranscripts(),
            _clock,
            new RefreshOptions(interval));

    private static int IssueCount(IssueSidebarViewModel sidebar) => sidebar.Groups.SelectMany(group => group.Items).Count();

    [Test]
    public async Task ShowsANewIssueWithinOneInterval()
    {
        var sidebar = CreateSidebar(Interval);
        await sidebar.RefreshCommand.ExecuteAsync(null);
        _gitHub.Issues = [.. _gitHub.Issues, Issues.Create(5, "Another", "acme/memento")];

        _clock.Advance(Interval);

        await AsyncAssert.EventuallyAsync(() => IssueCount(sidebar) == 2, "the new issue to show up");
    }

    [Test]
    public async Task DoesNotShowALoadingStateOrDisturbSearchAndSelection()
    {
        var sidebar = CreateSidebar(Interval);
        await sidebar.RefreshCommand.ExecuteAsync(null);
        sidebar.SelectedItem = sidebar.Groups.SelectMany(group => group.Items).Single();
        sidebar.SearchText = "notif";
        var loading = new List<bool>();
        sidebar.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IssueSidebarViewModel.IsLoading)) { loading.Add(sidebar.IsLoading); } };
        _gitHub.Issues = [.. _gitHub.Issues, Issues.Create(5, "Notify more", "acme/memento")];

        _clock.Advance(Interval);

        await AsyncAssert.EventuallyAsync(() => IssueCount(sidebar) == 2, "the refresh to finish");
        Assert.That(loading, Is.Empty);
        Assert.That(sidebar.SearchText, Is.EqualTo("notif"));
        Assert.That(sidebar.SelectedItem?.Issue.Number, Is.EqualTo(4));
    }

    [Test]
    public async Task KeepsTheCurrentListWhenABackgroundRefreshFails()
    {
        var sidebar = CreateSidebar(Interval);
        await sidebar.RefreshCommand.ExecuteAsync(null);
        _gitHub.FailWith = new InvalidOperationException("offline");

        _clock.Advance(Interval);
        await Task.Delay(100);

        Assert.That(IssueCount(sidebar), Is.EqualTo(1));
        Assert.That(sidebar.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task DoesNotRefreshWhenTheIntervalIsZero()
    {
        var sidebar = CreateSidebar(TimeSpan.Zero);
        await sidebar.RefreshCommand.ExecuteAsync(null);
        _gitHub.Issues = [.. _gitHub.Issues, Issues.Create(5, "Another", "acme/memento")];

        _clock.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(100);

        Assert.That(IssueCount(sidebar), Is.EqualTo(1));
    }

    [Test]
    public async Task NoticesWhenASessionIsClearedElsewhere()
    {
        var issue = _gitHub.Issues[0];
        _workspaces.Found.Add(new ExistingWorkspace(issue.Repository, issue.Number, new WorktreeInfo(@"C:\wt\4", "kontext/4-add", "abc", IsMain: false)));
        var sidebar = CreateSidebar(Interval);
        await sidebar.RefreshCommand.ExecuteAsync(null);
        Assert.That(_tracker.StateOf(issue.Key).Status, Is.EqualTo(IssueWorkStatus.Ready));

        _workspaces.Found.Clear();
        _clock.Advance(Interval);

        await AsyncAssert.EventuallyAsync(() => _tracker.StateOf(issue.Key).Status == IssueWorkStatus.NotStarted, "the cleared session to be noticed");
    }

    [Test]
    public async Task StopsRefreshingAfterSignOut()
    {
        var sidebar = CreateSidebar(Interval);
        await sidebar.RefreshCommand.ExecuteAsync(null);

        sidebar.Clear();
        _clock.Advance(Interval * 3);
        await Task.Delay(100);

        Assert.That(IssueCount(sidebar), Is.Zero);
    }
}
