using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class IssueSidebarViewModelTests
{
    private InMemoryIssueWorkTracker _tracker = null!;
    private FakeGitHubService _gitHub = null!;
    private FakeExistingWorkspaceFinder _finder = null!;
    private InMemoryHiddenRepositoryStore _hiddenStore = null!;
    private IssueSidebarViewModel _sidebar = null!;

    private InMemoryCollapsedRepositoryStore _collapsedStore = null!;
    private FakeSessionStore _sessionStore = null!;

    private IssueSidebarViewModel CreateSidebarWith(InMemoryHiddenRepositoryStore hiddenStore, InMemoryCollapsedRepositoryStore? collapsedStore = null) =>
        new(_gitHub, _tracker, _finder, new WorkspaceIssues(_gitHub), hiddenStore, collapsedStore ?? _collapsedStore, _sessionStore, new FixedTimeProvider(DateTimeOffset.Parse("2025-03-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));

    [SetUp]
    public void CreateSidebar()
    {
        _hiddenStore = new InMemoryHiddenRepositoryStore();
        _collapsedStore = new InMemoryCollapsedRepositoryStore();
        _sessionStore = new FakeSessionStore();
        _tracker = new InMemoryIssueWorkTracker();
        _gitHub = new FakeGitHubService();
        _finder = new FakeExistingWorkspaceFinder();

        _gitHub.Issues =
        [
            Issues.Create(4, "Add notifications", "acme/memento"),
            Issues.Create(3, "Bring project under UI test", "acme/memento"),
            Issues.Create(159, "Creating Galleries", "acme/gallery", labels: ["enhancement"]),
        ];

        _sidebar = CreateSidebarWith(_hiddenStore);
    }

    [TearDown]
    public void DisposeSidebar() => _sidebar.Dispose();

    private Task Refresh() => _sidebar.RefreshCommand.ExecuteAsync(null);

    [Test]
    public async Task ShowsTheErrorAndStopsLoadingWhenGitHubTimesOutWithoutTheUserCancelling()
    {
        _gitHub.FailWith = new TaskCanceledException("The request timed out.");

        await Refresh();

        Assert.That(_sidebar.ErrorMessage, Is.EqualTo("The request timed out."));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task ListsAnIssueOnceWhenPagingReturnsItTwice()
    {
        _gitHub.Issues = [.. _gitHub.Issues, _gitHub.Issues[0]];

        await Refresh();

        Assert.That(_sidebar.ErrorMessage, Is.Null);
        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Count(item => item.Issue.Number == 4), Is.EqualTo(1));
    }

    [Test]
    public async Task GroupsOpenIssuesByRepository()
    {
        await Refresh();

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").Items, Has.Count.EqualTo(2));
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task HidingARepositoryRemovesItsGroupButKeepsItInTheFilterList()
    {
        await Refresh();

        _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible = false;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery")));
        Assert.That(_sidebar.RepositoryFilters.Select(filter => filter.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(_sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible, Is.False);
    }

    [Test]
    public async Task ShowingAHiddenRepositoryAgainBringsItsGroupBack()
    {
        await Refresh();
        var memento = _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento");
        memento.IsVisible = false;

        memento.IsVisible = true;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(_hiddenStore.Hidden, Is.Empty);
    }

    [Test]
    public async Task RemembersTheHiddenRepositoriesInTheStore()
    {
        await Refresh();

        _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/gallery").IsVisible = false;

        Assert.That(_hiddenStore.Hidden, Is.EquivalentTo(Expect.Seq("acme/gallery")));
    }

    [Test]
    public async Task ShowsEveryRepositoryExpandedAtFirst()
    {
        await Refresh();

        Assert.That(_sidebar.Groups.Select(group => group.IsExpanded), Has.All.True);
    }

    [Test]
    public async Task RemembersARepositoryThatWasCollapsed()
    {
        await Refresh();

        _sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded = false;

        Assert.That(_collapsedStore.Collapsed, Is.EquivalentTo(Expect.Seq("acme/memento")));
    }

    [Test]
    public async Task ForgetsARepositoryThatWasExpandedAgain()
    {
        await Refresh();
        var group = _sidebar.Groups.Single(candidate => candidate.Name == "acme/memento");
        group.IsExpanded = false;

        group.IsExpanded = true;

        Assert.That(_collapsedStore.Collapsed, Is.Empty);
    }

    [Test]
    public async Task DoesNotSaveWhenTheStateDidNotChange()
    {
        await Refresh();

        _sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded = true;

        Assert.That(_collapsedStore.Saves, Is.Zero);
    }

    [Test]
    public async Task StartsWithTheRepositoriesTheStoreSaysAreCollapsed()
    {
        using var sidebar = CreateSidebarWith(_hiddenStore, new InMemoryCollapsedRepositoryStore("acme/memento"));

        await sidebar.RefreshCommand.ExecuteAsync(null);

        Assert.That(sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded, Is.False);
        Assert.That(sidebar.Groups.Single(group => group.Name == "acme/gallery").IsExpanded, Is.True);
    }

    [Test]
    public async Task KeepsARepositoryCollapsedWhileFilteringAndComingBack()
    {
        await Refresh();
        _sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded = false;

        _sidebar.SearchText = "Galleries";
        _sidebar.SearchText = string.Empty;

        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded, Is.False);
    }

    [Test]
    public async Task StartsWithTheRepositoriesTheStoreSaysAreHidden()
    {
        using var sidebar = CreateSidebarWith(new InMemoryHiddenRepositoryStore("acme/memento"));

        await sidebar.RefreshCommand.ExecuteAsync(null);

        Assert.That(sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery")));
        Assert.That(sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible, Is.False);
    }

    [Test]
    public async Task KeepsARepositoryHiddenAcrossARefresh()
    {
        await Refresh();
        _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible = false;

        await Refresh();

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery")));
        Assert.That(_sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible, Is.False);
    }

    [Test]
    public async Task CountsOnlyTheIssuesOfRepositoriesThatAreShown()
    {
        await Refresh();

        _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible = false;

        Assert.That(_sidebar.CountText, Is.EqualTo("1 open issue"));
    }

    [Test]
    public async Task ShowsThatAFilterIsActiveOnlyWhileARepositoryIsHidden()
    {
        await Refresh();
        Assert.That(_sidebar.IsFilterActive, Is.False);

        var memento = _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento");
        memento.IsVisible = false;

        Assert.That(_sidebar.IsFilterActive, Is.True);

        memento.IsVisible = true;

        Assert.That(_sidebar.IsFilterActive, Is.False);
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task ClearingTheFiltersShowsEveryRepositoryAgainAndForgetsThemInTheStore()
    {
        await Refresh();
        foreach (var filter in _sidebar.RepositoryFilters)
        {
            filter.IsVisible = false;
        }

        _sidebar.ClearFiltersCommand.Execute(null);

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(_sidebar.RepositoryFilters, Has.All.Property(nameof(RepositoryFilterItemViewModel.IsVisible)).True);
        Assert.That(_sidebar.IsFilterActive, Is.False);
        Assert.That(_sidebar.IsEverythingHidden, Is.False);
        Assert.That(_hiddenStore.Hidden, Is.Empty);
    }

    [Test]
    public async Task SaysEverythingIsHiddenInsteadOfNoOpenIssuesWhenEveryRepositoryIsHidden()
    {
        await Refresh();

        foreach (var filter in _sidebar.RepositoryFilters)
        {
            filter.IsVisible = false;
        }

        Assert.That(_sidebar.Groups, Is.Empty);
        Assert.That(_sidebar.IsEverythingHidden, Is.True);
        Assert.That(_sidebar.IsEmpty, Is.False);
    }

    [Test]
    public async Task StartsEveryIssueAsNotStarted()
    {
        await Refresh();

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Select(item => item.Status), Has.All.EqualTo(IssueWorkStatus.NotStarted));
    }

    [Test]
    public async Task ShowsAStatusFromTheTrackerForIssuesAlreadyInProgress()
    {
        _tracker.Report(new IssueKey("acme/memento", 4), new IssueWorkState(IssueWorkStatus.Running));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Running));
        Assert.That(item.StatusText, Is.EqualTo("Running"));
    }

    [Test]
    public async Task UpdatesAnItemLiveWhenTheTrackerReportsAChange()
    {
        await Refresh();
        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 159);
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _tracker.Report(new IssueKey("acme/gallery", 159), new IssueWorkState(IssueWorkStatus.NeedsInput));

        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.NeedsInput));
        Assert.That(changed, Does.Contain(nameof(IssueItemViewModel.Status)));
        Assert.That(changed, Does.Contain(nameof(IssueItemViewModel.StatusText)));
    }

    [Test]
    public async Task IgnoresTrackerChangesForIssuesThatAreNotListed()
    {
        await Refresh();

        Assert.That(() => _tracker.Report(new IssueKey("acme/unknown", 1), new IssueWorkState(IssueWorkStatus.Done)), Throws.Nothing);
    }

    [Test]
    public async Task FiltersWithTheSearchTextAndShowsHowManyMatch()
    {
        await Refresh();

        _sidebar.SearchText = "galleries";

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery")));
        Assert.That(_sidebar.CountText, Is.EqualTo("1 of 3 open issues"));
    }

    [Test]
    public async Task ShowsEverythingAgainWhenTheSearchIsCleared()
    {
        await Refresh();
        _sidebar.SearchText = "galleries";

        _sidebar.SearchText = string.Empty;

        Assert.That(_sidebar.Groups, Has.Count.EqualTo(2));
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task RemembersWhichGroupTheUserCollapsedAcrossSearches()
    {
        await Refresh();
        _sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded = false;

        _sidebar.SearchText = "notifications";
        _sidebar.SearchText = string.Empty;

        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded, Is.False);
    }

    [Test]
    public async Task ShowsAWorktreeWithASavedSessionAsNeedingInputButStillLetsItResume()
    {
        var key = new IssueKey("acme/memento", 4);
        _finder.Found.Add(new ExistingWorkspace(new RepositoryRef("acme", "memento"), 4, new WorktreeInfo(@"C:\wt\4", "kontext/4-add-notifications", "abc", IsMain: false)));
        _sessionStore.Save(new StoredSession(key, TerminalSessionId.New(), @"C:\wt\4", "kontext/4-add-notifications"));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.NeedsInput));
        Assert.That(_tracker.StateOf(key).Status, Is.EqualTo(IssueWorkStatus.Ready), "the tracked state stays Ready so selecting the issue still resumes it");
    }

    [Test]
    public async Task ShowsTheDotAsReadyAgainOnceTheSavedSessionIsCleared()
    {
        var key = new IssueKey("acme/memento", 4);
        _finder.Found.Add(new ExistingWorkspace(new RepositoryRef("acme", "memento"), 4, new WorktreeInfo(@"C:\wt\4", "kontext/4-add-notifications", "abc", IsMain: false)));
        _sessionStore.Save(new StoredSession(key, TerminalSessionId.New(), @"C:\wt\4", "kontext/4-add-notifications"));
        await Refresh();

        _sessionStore.Remove(key);
        _tracker.Report(key, IssueWorkState.NotStarted);
        _tracker.Report(key, new IssueWorkState(IssueWorkStatus.Ready, "kontext/4-add-notifications", @"C:\wt\4"));

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public async Task KeepsCollapsedGroupCollapsedAcrossRefresh()
    {
        await Refresh();
        _sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded = false;

        await Refresh();

        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").IsExpanded, Is.False);
        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/gallery").IsExpanded, Is.True);
    }

    [Test]
    public async Task RefreshUpdatesGroupsInPlaceWithoutResettingOrEmptyingThem()
    {
        await Refresh();
        var groupsBefore = _sidebar.Groups.ToList();
        var actions = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        var emptied = false;
        _sidebar.Groups.CollectionChanged += (_, e) => actions.Add(e.Action);
        foreach (var group in groupsBefore)
        {
            group.Items.CollectionChanged += (_, e) =>
            {
                actions.Add(e.Action);
                emptied |= group.Items.Count == 0;
            };
        }

        await Refresh();

        Assert.That(actions, Has.No.Member(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        Assert.That(emptied, Is.False);
        Assert.That(_sidebar.Groups, Is.EqualTo(groupsBefore));
    }

    [Test]
    public async Task ReordersItemsInPlaceWhenActivityChanges()
    {
        var earlier = DateTimeOffset.Parse("2025-02-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var later = DateTimeOffset.Parse("2025-02-20T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        _gitHub.Issues =
        [
            Issues.Create(4, "Add notifications", "acme/memento", updatedAt: later),
            Issues.Create(3, "Bring project under UI test", "acme/memento", updatedAt: earlier),
        ];
        await Refresh();
        var group = _sidebar.Groups.Single();

        _gitHub.Issues =
        [
            Issues.Create(4, "Add notifications", "acme/memento", updatedAt: earlier),
            Issues.Create(3, "Bring project under UI test", "acme/memento", updatedAt: later),
        ];
        await Refresh();

        Assert.That(_sidebar.Groups.Single(), Is.SameAs(group));
        Assert.That(group.Items.Select(item => item.Issue.Number), Is.EqualTo(Expect.Seq(3, 4)));
    }

    [Test]
    public async Task MarksWorktreesFromEarlierRunsAsReady()
    {
        _finder.Found.Add(new ExistingWorkspace(new RepositoryRef("acme", "memento"), 4, new WorktreeInfo(@"C:\wt\4", "kontext/4-add-notifications", "abc", IsMain: false)));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Ready));
        Assert.That(item.State.BranchName, Is.EqualTo("kontext/4-add-notifications"));
        Assert.That(item.State.WorktreePath, Is.EqualTo(@"C:\wt\4"));
    }

    [Test]
    public async Task DoesNotDowngradeAnIssueThatIsAlreadyRunning()
    {
        _tracker.Report(new IssueKey("acme/memento", 4), new IssueWorkState(IssueWorkStatus.Running));
        _finder.Found.Add(new ExistingWorkspace(new RepositoryRef("acme", "memento"), 4, new WorktreeInfo(@"C:\wt\4", "kontext/4-x", "abc", IsMain: false)));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 4)).Status, Is.EqualTo(IssueWorkStatus.Running));
    }

    private static ExistingWorkspace Workspace(int number, string repository = "acme/memento")
    {
        var parts = repository.Split('/');
        return new ExistingWorkspace(new RepositoryRef(parts[0], parts[1]), number, new WorktreeInfo($@"C:\wt\{number}", $"kontext/{number}-x", "abc", IsMain: false));
    }

    private static GitHubIssue Closed(int number, string title = "Old work", string repository = "acme/memento")
        => Issues.Create(number, title, repository) with { State = "closed" };

    [Test]
    public async Task IgnoresAWorktreeWhoseIssueGitHubCannotProvide()
    {
        _finder.Found.Add(Workspace(99));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 99)).Status, Is.EqualTo(IssueWorkStatus.NotStarted));
        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 99), Is.False);
    }

    [Test]
    public async Task KeepsAClosedIssueListedAsDoneWhileItsWorktreeRemains()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 7);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Done));
        Assert.That(item.State.WorktreePath, Is.EqualTo(@"C:\wt\7"));
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues · 1 closed"));
    }

    [Test]
    public async Task ListsAClosedIssueInItsOwnRepositoryGroup()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));

        await Refresh();

        Assert.That(_sidebar.Groups.Single(group => group.Name == "acme/memento").Items.Select(item => item.Issue.Number), Does.Contain(7));
    }

    [Test]
    public async Task DropsAClosedIssueOnceItsWorktreeIsGone()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        await Refresh();

        _finder.Found.Clear();
        await Refresh();

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 7), Is.False);
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task DropsAClosedIssueAndItsSelectionTheMomentItsSessionIsCleared()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        await Refresh();
        var key = new IssueKey("acme/memento", 7);
        _sidebar.SelectedItem = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 7);

        _tracker.Report(key, IssueWorkState.NotStarted);

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 7), Is.False);
        Assert.That(_sidebar.SelectedItem, Is.Null);
        Assert.That(_sidebar.CountText, Is.EqualTo("3 open issues"));
    }

    [Test]
    public async Task KeepsAnOpenIssueListedWhenItsSessionIsCleared()
    {
        await Refresh();
        var key = new IssueKey("acme/memento", 4);
        _tracker.Report(key, new IssueWorkState(IssueWorkStatus.Ready, "features/4-x", @"C:\wt\4"));

        _tracker.Report(key, IssueWorkState.NotStarted);

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 4), Is.True);
    }

    [Test]
    public async Task NeverListsAClosedIssueThatHasNoWorktree()
    {
        _gitHub.ClosedIssues.Add(Closed(7));

        await Refresh();

        Assert.That(_sidebar.Groups.SelectMany(group => group.Items).Any(item => item.Issue.Number == 7), Is.False);
    }

    [Test]
    public async Task TurnsAReadyIssueIntoDoneWhenItWasClosedMeanwhile()
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        _tracker.Report(new IssueKey("acme/memento", 7), new IssueWorkState(IssueWorkStatus.Ready, "kontext/7-x", @"C:\wt\7"));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 7)).Status, Is.EqualTo(IssueWorkStatus.Done));
    }

    [TestCase(IssueWorkStatus.Running)]
    [TestCase(IssueWorkStatus.NeedsInput)]
    public async Task LeavesAClosedIssueWithALiveSessionToWhatTheSessionReports(IssueWorkStatus live)
    {
        _gitHub.ClosedIssues.Add(Closed(7));
        _finder.Found.Add(Workspace(7));
        _tracker.Report(new IssueKey("acme/memento", 7), new IssueWorkState(live));

        await Refresh();

        Assert.That(_tracker.StateOf(new IssueKey("acme/memento", 7)).Status, Is.EqualTo(live));
    }

    [Test]
    public async Task ShowsAnOpenIssueMissingFromTheCappedListWhenItHasAWorktree()
    {
        _gitHub.ClosedIssues.Add(Issues.Create(500, "Beyond the cap", "acme/memento"));
        _finder.Found.Add(Workspace(500));

        await Refresh();

        var item = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 500);
        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.Ready), "it is open, so it is ready, not done");
    }

    [Test]
    public async Task KeepsTheSameIssueSelectedAcrossARefreshPointingAtTheReloadedItem()
    {
        await Refresh();
        var before = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);
        _sidebar.SelectedItem = before;

        await Refresh();

        Assert.That(_sidebar.SelectedItem, Is.Not.Null);
        Assert.That(_sidebar.SelectedItem!.Issue.Number, Is.EqualTo(4));
        Assert.That(_sidebar.SelectedItem, Is.Not.SameAs(before));
    }

    [Test]
    public async Task ClearsTheSelectionWhenTheSelectedIssueDisappears()
    {
        await Refresh();
        _sidebar.SelectedItem = _sidebar.Groups.SelectMany(group => group.Items).Single(candidate => candidate.Issue.Number == 4);

        _gitHub.Issues = [Issues.Create(159, "Creating Galleries", "acme/gallery")];
        await Refresh();

        Assert.That(_sidebar.SelectedItem, Is.Null);
    }

    [Test]
    public async Task ShowsTheErrorAndNoIssuesWhenLoadingFails()
    {
        _gitHub.FailWith = new TestKontextException("GitHub is down");

        await Refresh();

        Assert.That(_sidebar.ErrorMessage, Is.EqualTo("GitHub is down"));
        Assert.That(_sidebar.HasError, Is.True);
        Assert.That(_sidebar.Groups, Is.Empty);
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task RecoversOnTheNextSuccessfulRefresh()
    {
        _gitHub.FailWith = new TestKontextException("GitHub is down");
        await Refresh();
        _gitHub.FailWith = null;

        await Refresh();

        Assert.That(_sidebar.HasError, Is.False);
        Assert.That(_sidebar.Groups, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ShowsTheEmptyStateWhenThereAreNoOpenIssues()
    {
        _gitHub.Issues = [];

        await Refresh();

        Assert.That(_sidebar.IsEmpty, Is.True);
        Assert.That(_sidebar.CountText, Is.Empty);
    }

    [Test]
    public async Task IgnoresAStaleLoadThatArrivesBeforeTheNewerOne()
    {
        _gitHub.Deferred = true;
        var older = Refresh();
        var newer = Refresh();
        Assert.That(_sidebar.IsLoading, Is.True);

        _gitHub.Complete(0, [Issues.Create(1, "From the older load", "acme/old")]);
        await older;
        Assert.That(_sidebar.Groups, Is.Empty, "the superseded result must not be shown");
        Assert.That(_sidebar.IsLoading, Is.True, "the newer load still owns the loading state");

        _gitHub.Complete(1, [Issues.Create(2, "From the newer load", "acme/new")]);
        await newer;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/new")));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task IgnoresAStaleLoadThatArrivesAfterTheNewerOne()
    {
        _gitHub.Deferred = true;
        var older = Refresh();
        var newer = Refresh();

        _gitHub.Complete(1, [Issues.Create(2, "From the newer load", "acme/new")]);
        await newer;

        _gitHub.Complete(0, [Issues.Create(1, "From the older load", "acme/old")]);
        await older;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/new")));
        Assert.That(_sidebar.IsLoading, Is.False);
    }

    [Test]
    public async Task ClearForgetsEverythingLoadedForThePreviousUser()
    {
        await Refresh();
        _sidebar.SelectedItem = _sidebar.Groups.First().Items.First();
        _sidebar.SearchText = "x";

        _sidebar.Clear();

        Assert.That(_sidebar.Groups, Is.Empty);
        Assert.That(_sidebar.SelectedItem, Is.Null);
        Assert.That(_sidebar.SearchText, Is.Empty);
        Assert.That(_sidebar.CountText, Is.Empty);
        Assert.That(_sidebar.HasError, Is.False);
    }

    [Test]
    public async Task StopsListeningToTheTrackerAfterDisposal()
    {
        await Refresh();
        var item = _sidebar.Groups.SelectMany(group => group.Items).First();
        _sidebar.Dispose();

        _tracker.Report(item.Key, new IssueWorkState(IssueWorkStatus.Done));

        Assert.That(item.Status, Is.EqualTo(IssueWorkStatus.NotStarted));
    }

    private const string Enterprise = "ghe.example.com";

    private static GitHubIssue EnterpriseIssue(int number, string title, string repository)
    {
        var parts = repository.Split('/');
        return Issues.Create(number, title, repository) with { Repository = new RepositoryRef(parts[0], parts[1], Host: Enterprise) };
    }

    private async Task RefreshWithAnEnterpriseRepository()
    {
        _gitHub.Issues = [.. _gitHub.Issues, EnterpriseIssue(7, "Rate limit the API", "team/api")];
        await Refresh();
    }

    private RepositoryAccountFilterViewModel Account(string host) =>
        _sidebar.RepositoryAccounts.Single(account => string.Equals(account.Host, host, StringComparison.OrdinalIgnoreCase));

    [Test]
    public async Task GroupsTheRepositoryFiltersByAccountWithGitHubFirst()
    {
        await RefreshWithAnEnterpriseRepository();

        Assert.That(_sidebar.RepositoryAccounts.Select(account => account.Host), Is.EqualTo(Expect.Seq(GitHubHosts.Default, Enterprise)));
        Assert.That(Account(GitHubHosts.Default).Repositories.Select(filter => filter.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(Account(Enterprise).Repositories.Select(filter => filter.Name), Is.EqualTo(Expect.Seq($"{Enterprise}/team/api")));
    }

    [Test]
    public async Task ShowsEnterpriseRepositoriesWithoutTheServerInFrontOfTheirName()
    {
        await RefreshWithAnEnterpriseRepository();

        Assert.That(Account(Enterprise).Repositories.Single().DisplayName, Is.EqualTo("team/api"));
        Assert.That(Account(GitHubHosts.Default).Repositories.Select(filter => filter.DisplayName), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
    }

    [Test]
    public async Task ShowsTheSignedInUsernameOnTheAccountCard()
    {
        _sidebar.SetAccountLogin(GitHubHosts.Default, "octocat");
        _sidebar.SetAccountLogin(Enterprise, "jelle.buning");

        await RefreshWithAnEnterpriseRepository();

        Assert.That(Account(GitHubHosts.Default).Title, Is.EqualTo("octocat - github.com"));
        Assert.That(Account(Enterprise).Title, Is.EqualTo($"jelle.buning - {Enterprise}"));
    }

    [Test]
    public async Task UpdatesTheUsernameOnAnOpenAccountCardWhenTheUserSignsIn()
    {
        await Refresh();
        var account = Account(GitHubHosts.Default);
        var changed = new List<string?>();
        account.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _sidebar.SetAccountLogin(GitHubHosts.Default, "octocat");

        Assert.That(account.Title, Is.EqualTo("octocat - github.com"));
        Assert.That(changed, Does.Contain(nameof(RepositoryAccountFilterViewModel.Title)));
    }

    [Test]
    public async Task UncheckingAnAccountHidesAllItsRepositoriesAndRemembersThem()
    {
        await RefreshWithAnEnterpriseRepository();

        Account(GitHubHosts.Default).AreAllVisible = false;

        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq($"{Enterprise}/team/api")));
        Assert.That(Account(GitHubHosts.Default).Repositories, Has.All.Property(nameof(RepositoryFilterItemViewModel.IsVisible)).False);
        Assert.That(_hiddenStore.Hidden, Is.EquivalentTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(_hiddenStore.Saves, Is.EqualTo(1), "one save for the whole account, not one per repository");
    }

    [Test]
    public async Task CheckingAnAccountShowsAllItsRepositoriesAgain()
    {
        await RefreshWithAnEnterpriseRepository();
        Account(GitHubHosts.Default).AreAllVisible = false;

        Account(GitHubHosts.Default).AreAllVisible = true;

        Assert.That(_sidebar.Groups, Has.Count.EqualTo(3));
        Assert.That(_hiddenStore.Hidden, Is.Empty);
    }

    [Test]
    public async Task ShowsTheAccountAsPartlyCheckedWhenOnlySomeOfItsRepositoriesAreShown()
    {
        await RefreshWithAnEnterpriseRepository();
        var account = Account(GitHubHosts.Default);
        var changed = new List<string?>();
        account.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        account.Repositories.Single(filter => filter.Name == "acme/memento").IsVisible = false;

        Assert.That(account.AreAllVisible, Is.Null);
        Assert.That(changed, Does.Contain(nameof(RepositoryAccountFilterViewModel.AreAllVisible)));
        Assert.That(Account(Enterprise).AreAllVisible, Is.True, "other accounts are not affected");
    }

    [Test]
    public async Task UpdatesTheRepositoryCheckboxesWhenTheirAccountIsUnchecked()
    {
        await Refresh();
        var memento = Account(GitHubHosts.Default).Repositories.Single(filter => filter.Name == "acme/memento");
        var changed = new List<string?>();
        memento.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Account(GitHubHosts.Default).AreAllVisible = false;

        Assert.That(changed, Does.Contain(nameof(RepositoryFilterItemViewModel.IsVisible)));
    }

    [Test]
    public async Task ShowsTheServerAsTitleForAnAccountWithoutAKnownUsername()
    {
        await RefreshWithAnEnterpriseRepository();

        Assert.That(Account(Enterprise).Title, Is.EqualTo(Enterprise));
    }

    private RepositoryGroupViewModel MenuGroup(string name) => _sidebar.MenuGroups.Single(group => group.Name == name);

    [Test]
    public async Task KeepsAHiddenRepositoryInTheMenuButHidesItSoTheMenuNeverRebuildsItsRow()
    {
        await Refresh();
        var memento = MenuGroup("acme/memento");

        _sidebar.RepositoryFilters.Single(filter => filter.Name == "acme/memento").IsVisible = false;

        Assert.That(_sidebar.MenuGroups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery", "acme/memento")));
        Assert.That(MenuGroup("acme/memento"), Is.SameAs(memento));
        Assert.That(memento.IsVisible, Is.False);
        Assert.That(MenuGroup("acme/gallery").IsVisible, Is.True);
        Assert.That(_sidebar.Groups.Select(group => group.Name), Is.EqualTo(Expect.Seq("acme/gallery")), "Groups still lists only what is shown");
    }

    [Test]
    public async Task TogglingARepositoryFilterOnlyFlipsVisibilityAndKeepsTheExpandState()
    {
        await Refresh();
        MenuGroup("acme/memento").IsExpanded = false;
        var filter = _sidebar.RepositoryFilters.Single(candidate => candidate.Name == "acme/memento");
        var menuChanges = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        _sidebar.MenuGroups.CollectionChanged += (_, e) => menuChanges.Add(e.Action);

        filter.IsVisible = false;
        filter.IsVisible = true;

        Assert.That(menuChanges, Is.Empty, "no row is removed or inserted, so the menu has nothing to rebuild");
        Assert.That(MenuGroup("acme/memento").IsVisible, Is.True);
        Assert.That(MenuGroup("acme/memento").IsExpanded, Is.False);
        Assert.That(MenuGroup("acme/gallery").IsExpanded, Is.True);
    }

    [Test]
    public async Task HidesGroupsWithoutAMatchWhileSearchingAndLeavesTheirIssuesInPlace()
    {
        await Refresh();
        var memento = MenuGroup("acme/memento");
        var emptied = false;
        memento.Items.CollectionChanged += (_, _) => emptied |= memento.Items.Count == 0;

        _sidebar.SearchText = "galleries";

        Assert.That(memento.IsVisible, Is.False);
        Assert.That(emptied, Is.False, "a group whose children run out gets collapsed by the menu");
        Assert.That(memento.Items, Has.Count.EqualTo(2));

        _sidebar.SearchText = string.Empty;

        Assert.That(memento.IsVisible, Is.True);
    }

    [Test]
    public async Task ForgetsTheMenuGroupsWhenCleared()
    {
        await Refresh();

        _sidebar.Clear();

        Assert.That(_sidebar.MenuGroups, Is.Empty);
    }
}
