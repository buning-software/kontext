using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class IssueSidebarViewModel : ObservableObject, IDisposable
{
    private readonly IGitHubService _gitHub;
    private readonly IIssueWorkTracker _tracker;
    private readonly IExistingWorkspaceFinder _existingWorkspaces;
    private readonly IWorkspaceIssues _workspaceIssues;
    private readonly IHiddenRepositoryStore _hiddenRepositories;
    private readonly ICollapsedRepositoryStore _collapsedRepositories;
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _clock;
    private readonly RefreshOptions _refreshOptions;
    private readonly HashSet<string> _hidden;
    private readonly HashSet<string> _collapsed;
    private readonly LatestOnly _loads = new();
    private readonly Dictionary<string, RepositoryGroupViewModel> _groupsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _loginsByHost = new(StringComparer.OrdinalIgnoreCase);

    private List<IssueItemViewModel> _all = [];
    private Dictionary<IssueKey, IssueItemViewModel> _itemsByKey = [];
    private CancellationTokenSource? _autoRefresh;
    private int _removals;
    private bool _loadedOnce;

    public IssueSidebarViewModel(
        IGitHubService gitHub,
        IIssueWorkTracker tracker,
        IExistingWorkspaceFinder existingWorkspaces,
        IWorkspaceIssues workspaceIssues,
        IHiddenRepositoryStore hiddenRepositories,
        ICollapsedRepositoryStore collapsedRepositories,
        ISessionStore sessions,
        TimeProvider clock,
        RefreshOptions? refreshOptions = null)
    {
        _gitHub = gitHub;
        _tracker = tracker;
        _existingWorkspaces = existingWorkspaces;
        _workspaceIssues = workspaceIssues;
        _hiddenRepositories = hiddenRepositories;
        _collapsedRepositories = collapsedRepositories;
        _sessions = sessions;
        _clock = clock;
        _refreshOptions = refreshOptions ?? new RefreshOptions(TimeSpan.Zero);
        _hidden = new HashSet<string>(hiddenRepositories.Load(), StringComparer.OrdinalIgnoreCase);
        _collapsed = new HashSet<string>(collapsedRepositories.Load(), StringComparer.OrdinalIgnoreCase);

        _tracker.Changed += OnTrackerChanged;
    }

    /// <summary>The repository groups that are shown (not filtered out, with a search match).</summary>
    public ObservableCollection<RepositoryGroupViewModel> Groups { get; } = [];

    /// <summary>
    /// What the menu binds to: a group for every repository, filtered-out ones with <see cref="RepositoryGroupViewModel.IsVisible"/>
    /// false, so filtering and searching never remove or insert menu rows.
    /// </summary>
    public ObservableCollection<RepositoryGroupViewModel> MenuGroups { get; } = [];

    public ObservableCollection<RepositoryFilterItemViewModel> RepositoryFilters { get; } = [];

    /// <summary>The same filters as <see cref="RepositoryFilters"/>, as one card per account (github.com first).</summary>
    public ObservableCollection<RepositoryAccountFilterViewModel> RepositoryAccounts { get; } = [];

    [ObservableProperty]
    public partial IssueItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => _loadedOnce && !IsLoading && !HasError && _all.Count == 0;

    public bool IsEverythingHidden => _all.Count > 0 && _all.All(IsHidden);

    public bool HasNoRepositoryFilters => RepositoryFilters.Count == 0;

    public bool IsFilterActive => RepositoryFilters.Any(filter => !filter.IsVisible);

    public void Dispose()
    {
        _tracker.Changed -= OnTrackerChanged;
        StopAutoRefresh();
        _loads.Dispose();
    }

    public void Clear()
    {
        StopAutoRefresh();
        _loads.CancelCurrent();

        _all = [];
        _itemsByKey = [];
        _groupsByName.Clear();
        Groups.Clear();
        MenuGroups.Clear();
        RepositoryFilters.Clear();
        RepositoryAccounts.Clear();
        OnPropertyChanged(nameof(HasNoRepositoryFilters));
        OnPropertyChanged(nameof(IsFilterActive));

        _loadedOnce = false;

        SelectedItem = null;
        SearchText = string.Empty;
        IsLoading = false;
        CountText = string.Empty;
        ErrorMessage = null;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        StartAutoRefresh();
        await LoadAsync(silent: false);
    }

    private void StartAutoRefresh()
    {
        if (!_refreshOptions.IsEnabled || _autoRefresh is not null)
        {
            return;
        }

        _autoRefresh = new CancellationTokenSource();
        _ = RunAutoRefreshAsync(_autoRefresh.Token);
    }

    private void StopAutoRefresh()
    {
        _autoRefresh?.Cancel();
        _autoRefresh?.Dispose();
        _autoRefresh = null;
    }

    private async Task RunAutoRefreshAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(_refreshOptions.Interval, _clock);
            while (await timer.WaitForNextTickAsync(token))
            {
                await LoadAsync(silent: true);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadAsync(bool silent)
    {
        if (silent && IsLoading)
        {
            return;
        }

        var load = _loads.Begin();
        var token = load.Token;
        var removals = _removals;

        if (!silent)
        {
            IsLoading = true;
            ErrorMessage = null;
        }

        try
        {
            var open = await _gitHub.GetIssuesAsync(repository: null, token);
            var workspaces = await _existingWorkspaces.FindAllAsync(token);

            var withWorkspace = await _workspaceIssues.FindMissingAsync(open, workspaces, token);
            token.ThrowIfCancellationRequested();

            if (silent && removals != _removals)
            {
                return;
            }

            Show([.. open, .. withWorkspace]);
            MarkExistingWorkspaces(workspaces);
            _loadedOnce = true;
            ErrorMessage = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (!silent)
            {
                ErrorMessage = ex.Message;
                _all = [];
                _itemsByKey = [];
                RebuildRepositoryFilters();
                ApplyFilter();
            }
        }

        if (!silent && _loads.IsCurrent(load))
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void Show(IReadOnlyList<GitHubIssue> issues)
    {
        var now = _clock.GetUtcNow();
        var previouslySelected = SelectedItem?.Key;

        _all = issues.DistinctBy(issue => issue.Key).Select(issue => new IssueItemViewModel(issue, _tracker.StateOf(issue.Key), now)).ToList();
        _itemsByKey = _all.ToDictionary(item => item.Key);

        SelectedItem = previouslySelected is { } key && _itemsByKey.TryGetValue(key, out var reloaded) ? reloaded : null;

        RebuildRepositoryFilters();
        ApplyFilter();
    }

    private bool IsHidden(IssueItemViewModel item) => _hidden.Contains(item.Issue.Repository.DisplayName);

    /// <summary>Remembers who is signed in on <paramref name="host"/>, so its account card shows the username (null: signed out).</summary>
    public void SetAccountLogin(string host, string? login)
    {
        if (login is null)
        {
            _loginsByHost.Remove(host);
        }
        else
        {
            _loginsByHost[host] = login;
        }

        foreach (var account in RepositoryAccounts.Where(account => string.Equals(account.Host, host, StringComparison.OrdinalIgnoreCase)))
        {
            account.SetLogin(login);
        }
    }

    private void RebuildRepositoryFilters()
    {
        RepositoryFilters.Clear();
        RepositoryAccounts.Clear();

        var repositories = _all
            .Select(item => item.Issue.Repository)
            .DistinctBy(repository => repository.DisplayName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(repository => repository.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filters = repositories.ToDictionary(
            repository => repository.DisplayName,
            repository => new RepositoryFilterItemViewModel(repository.DisplayName, repository.FullName, isVisible: !_hidden.Contains(repository.DisplayName), OnRepositoryVisibilityChanged),
            StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters.Values)
        {
            RepositoryFilters.Add(filter);
        }

        var accounts = repositories
            .GroupBy(repository => repository.Host, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GitHubHosts.IsDefault(group.Key) ? 0 : 1)
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var account in accounts)
        {
            RepositoryAccounts.Add(new RepositoryAccountFilterViewModel(
                account.Key,
                _loginsByHost.GetValueOrDefault(account.Key),
                [.. account.Select(repository => filters[repository.DisplayName])],
                SetAllVisible));
        }

        OnPropertyChanged(nameof(HasNoRepositoryFilters));
    }

    private void SetAllVisible(RepositoryAccountFilterViewModel account, bool visible)
    {
        foreach (var filter in account.Repositories)
        {
            filter.ShowWithoutReporting(visible);

            if (visible)
            {
                _hidden.Remove(filter.Name);
            }
            else
            {
                _hidden.Add(filter.Name);
            }
        }

        _hiddenRepositories.Save(_hidden);
        account.RepositoriesChanged();
        ApplyFilter();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        if (_hidden.Count == 0)
        {
            return;
        }

        _hidden.Clear();
        _hiddenRepositories.Save(_hidden);

        RebuildRepositoryFilters();
        ApplyFilter();
    }

    private void OnRepositoryVisibilityChanged(RepositoryFilterItemViewModel filter)
    {
        if (filter.IsVisible)
        {
            _hidden.Remove(filter.Name);
        }
        else
        {
            _hidden.Add(filter.Name);
        }

        _hiddenRepositories.Save(_hidden);

        foreach (var account in RepositoryAccounts.Where(account => account.Repositories.Contains(filter)))
        {
            account.RepositoriesChanged();
        }

        ApplyFilter();
    }

    private void OnGroupExpandedChanged(RepositoryGroupViewModel group)
    {
        if (group.IsExpanded)
        {
            _collapsed.Remove(group.Name);
        }
        else
        {
            _collapsed.Add(group.Name);
        }

        _collapsedRepositories.Save(_collapsed);
    }

    private void MarkExistingWorkspaces(IReadOnlyList<ExistingWorkspace> workspaces)
    {
        foreach (var workspace in workspaces)
        {
            if (!_itemsByKey.TryGetValue(workspace.Issue, out var item))
            {
                continue;
            }

            item.HasSavedSession = _sessions.Find(workspace.Issue) is not null;

            var status = _tracker.StateOf(workspace.Issue).Status;
            var closed = item.Issue.IsClosed;

            var next = (status, closed) switch
            {
                (IssueWorkStatus.NotStarted or IssueWorkStatus.Ready, true) => IssueWorkStatus.Done,
                (IssueWorkStatus.NotStarted, false) => IssueWorkStatus.Ready,
                _ => status,
            };

            if (next != status)
            {
                _tracker.Report(workspace.Issue, new IssueWorkState(next, workspace.Worktree.BranchName, workspace.Worktree.Path));
            }
        }
    }

    private void ApplyFilter()
    {
        var visible = _all.Where(item => !IsHidden(item) && IssueSearch.Matches(item.Issue, SearchText)).ToList();

        var shown = IssueGrouper.Group(visible, item => item.Issue.Repository.DisplayName, item => item.Issue.UpdatedAt)
            .ToDictionary(group => group.Repository, StringComparer.OrdinalIgnoreCase);

        var repositories = _all
            .Select(item => item.Issue.Repository.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);

        var menu = new List<RepositoryGroupViewModel>();
        var desired = new List<RepositoryGroupViewModel>(shown.Count);
        foreach (var repository in repositories)
        {
            var group = shown.GetValueOrDefault(repository);
            if (!_groupsByName.TryGetValue(repository, out var viewModel))
            {
                viewModel = new RepositoryGroupViewModel(repository, group?.Items ?? [], !_collapsed.Contains(repository), OnGroupExpandedChanged);
                _groupsByName[repository] = viewModel;
            }
            else if (group is not null)
            {
                // A hidden group keeps its last issues: a menu group whose children run out gets collapsed by the menu.
                viewModel.SyncItems(group.Items);
            }

            viewModel.IsVisible = group is not null;
            menu.Add(viewModel);
            if (group is not null)
            {
                desired.Add(viewModel);
            }
        }

        CollectionSync.SyncInPlace(Groups, desired);

        // The menu keeps a row for every repository and only hides the filtered ones. Removing and re-inserting rows made
        // the NavigationView rebuild them, and a rebuilt row could show a different expand state than its group has.
        CollectionSync.SyncInPlace(MenuGroups, menu);

        CountText = BuildCountText(visible.Count);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsEverythingHidden));
        OnPropertyChanged(nameof(IsFilterActive));
    }

    private string BuildCountText(int visibleCount)
    {
        if (_all.Count == 0)
        {
            return string.Empty;
        }

        var shown = _all.Where(item => !IsHidden(item)).ToList();
        var closed = shown.Count(item => item.Issue.IsClosed);
        var open = shown.Count - closed;

        var issues = open == 1 ? "1 open issue" : $"{open} open issues";
        var text = string.IsNullOrWhiteSpace(SearchText) ? issues : $"{visibleCount} of {issues}";

        return closed == 0 ? text : $"{text} · {closed} closed";
    }

    private void OnTrackerChanged(object? sender, IssueWorkChangedEventArgs e)
    {
        if (!_itemsByKey.TryGetValue(e.Issue, out var item))
        {
            return;
        }

        item.State = e.State;

        if (e.State.Status == IssueWorkStatus.NotStarted)
        {
            item.HasSavedSession = false;
        }

        if (item.Issue.IsClosed && e.State.Status == IssueWorkStatus.NotStarted)
        {
            Remove(item);
        }
    }

    private void Remove(IssueItemViewModel item)
    {
        if (ReferenceEquals(SelectedItem, item))
        {
            SelectedItem = null;
        }

        _removals++;
        _all.Remove(item);
        _itemsByKey.Remove(item.Key);
        ApplyFilter();
    }
}
