using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Presentation;

/// <summary>
/// The uncommitted changes of one worktree, and committing and pushing them. It reloads when it becomes active and
/// then polls, but only while <see cref="IsActive"/> and a <see cref="WorktreePath"/> are both set. Await continuations
/// return to the thread that set those properties (the UI thread), so no dispatcher is needed.
/// </summary>
public sealed partial class GitChangesViewModel(IWorkspaceChanges changes, IWorkspaceCommitter committer, TimeProvider clock, GitChangesOptions options) : ObservableObject, IDisposable
{
    private readonly LatestOnly _reloads = new();
    private readonly LatestOnly _selections = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<string> _checked = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loop;
    private WorkingTreeChanges _last = WorkingTreeChanges.Empty;
    private FileChange? _selectedChange;
    private bool _disposed;

    [ObservableProperty]
    public partial string? WorktreePath { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<GitChangeRow> Rows { get; set; } = [];

    [ObservableProperty]
    public partial FileDiff? SelectedDiff { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<SplitDiffRow> SplitRows { get; set; } = [];

    /// <summary>Whether the diff is shown old-beside-new instead of as one column. It is a per-session choice, not saved.</summary>
    [ObservableProperty]
    public partial bool IsSplitView { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool HasLoaded { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    public partial string CommitMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand), nameof(PushCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>What the running commit or push is doing, e.g. "Pushing…".</summary>
    [ObservableProperty]
    public partial string BusyText { get; set; } = string.Empty;

    /// <summary>Why the last commit or push failed. Kept apart from <see cref="ErrorMessage"/> so a successful poll does not hide it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    public partial string? ActionError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PushCommand))]
    public partial int UnpushedCount { get; set; }

    public int CheckedCount => _checked.Count;

    /// <summary>The "all files" box: checked when every changed file is selected, unchecked when none is, otherwise undetermined.</summary>
    public bool? AllChecked
    {
        get
        {
            var total = DistinctPaths().Count();
            return total == 0 || _checked.Count == 0 ? false : _checked.Count >= total ? true : null;
        }
        set
        {
            if (IsBusy)
            {
                return;
            }

            _checked.Clear();
            if (value == true)
            {
                _checked.UnionWith(DistinctPaths());
            }

            foreach (var row in Rows)
            {
                row.SetCheckedFromModel(row.Change is { } change && _checked.Contains(change.Path));
            }

            RaiseCheckedChanged();
        }
    }

    public bool HasActionError => !string.IsNullOrEmpty(ActionError);

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public bool HasChanges => _last.Staged.Count + _last.Unstaged.Count > 0;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => HasLoaded && !HasChanges && !HasError;

    public bool HasSelectedChange => _selectedChange is not null;

    public bool NeedsSelection => HasChanges && _selectedChange is null;

    public FileChange? SelectedChange
    {
        get => _selectedChange;
        set
        {
            if (Select(value))
            {
                _ = LoadSelectedDiffAsync(_selections.Begin().Token);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        StopLoop();
        _reloads.Dispose();
        _selections.Dispose();
    }

    public async Task ReloadAsync()
    {
        var path = WorktreePath;
        if (_disposed || string.IsNullOrEmpty(path))
        {
            return;
        }

        var operation = _reloads.Begin();
        var token = operation.Token;

        try
        {
            // Starting git and reading files must not happen on the UI thread; the continuation comes back to it.
            var result = await Task.Run(() => changes.GetChangesAsync(path, token), token);
            var unpushed = await Task.Run(() => committer.CountUnpushedAsync(path, token), token);
            if (!IsCurrent(operation, path))
            {
                return;
            }

            Apply(result);
            UnpushedCount = unpushed;
            ErrorMessage = null;
            HasLoaded = true;
            await LoadSelectedDiffAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // Any git failure is shown to the user; the next poll tries again.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (IsCurrent(operation, path))
            {
                ErrorMessage = ex.Message;
            }
        }
    }

    partial void OnWorktreePathChanged(string? value)
    {
        StopLoop();

        _last = WorkingTreeChanges.Empty;
        _checked.Clear();
        RaiseCheckedChanged();
        CommitCommand.NotifyCanExecuteChanged();
        CommitMessage = string.Empty;
        ActionError = null;
        StatusMessage = null;
        UnpushedCount = 0;
        Rows = [];
        Summary = string.Empty;
        ErrorMessage = null;
        HasLoaded = false;
        Select(null);
        SelectedDiff = null;
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(IsEmpty));

        StartLoopIfNeeded();
    }

    partial void OnSelectedDiffChanged(FileDiff? value) => SplitRows = value is null ? [] : SplitDiffRows.Build(value.Lines);

    partial void OnIsActiveChanged(bool value)
    {
        StopLoop();
        StartLoopIfNeeded();
    }

    private bool IsCurrent(CancellationTokenSource operation, string path)
        => !_disposed && _reloads.IsCurrent(operation) && WorktreePath == path;

    private void StartLoopIfNeeded()
    {
        if (_disposed || !IsActive || string.IsNullOrEmpty(WorktreePath))
        {
            return;
        }

        var loop = _loop = new CancellationTokenSource();
        _ = RunLoopAsync(loop.Token);
    }

    private void StopLoop()
    {
        if (_loop is { } loop)
        {
            _loop = null;
            loop.Cancel();
            loop.Dispose();
        }

        _reloads.CancelCurrent();
        _selections.CancelCurrent();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            // Created before the first reload so a tick that arrives while it runs is not lost.
            using var timer = new PeriodicTimer(options.PollInterval, clock);

            await ReloadAsync();
            while (await timer.WaitForNextTickAsync(token))
            {
                // A commit or push reloads when it is done; reading git halfway through would show a transient state.
                if (!IsBusy)
                {
                    await ReloadAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Apply(WorkingTreeChanges result)
    {
        if (!result.Equals(_last))
        {
            _last = result;
            var present = result.Staged.Concat(result.Unstaged).Select(change => change.Path).ToHashSet(StringComparer.Ordinal);
            if (_checked.RemoveWhere(path => !present.Contains(path)) > 0)
            {
                RaiseCheckedChanged();
                CommitCommand.NotifyCanExecuteChanged();
            }

            Rows = BuildRows(result);
            OnPropertyChanged(nameof(AllChecked));
            Summary = BuildSummary(result);
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(IsEmpty));
        }

        var current = _selectedChange;
        var next = current is null
            ? null
            : result.Staged.Concat(result.Unstaged).FirstOrDefault(change => change.Path == current.Path && change.IsStaged == current.IsStaged);

        Select(next ?? FirstOf(result.Staged) ?? FirstOf(result.Unstaged));
    }

    private static FileChange? FirstOf(IReadOnlyList<FileChange> group) => group.Count > 0 ? group[0] : null;

    private bool Select(FileChange? value)
    {
        if (EqualityComparer<FileChange?>.Default.Equals(_selectedChange, value))
        {
            return false;
        }

        var sameFile = _selectedChange is { } previous && value is { } next && previous.Path == next.Path && previous.IsStaged == next.IsStaged;
        _selectedChange = value;

        if (!sameFile)
        {
            SelectedDiff = null;
        }

        OnPropertyChanged(nameof(SelectedChange));
        OnPropertyChanged(nameof(HasSelectedChange));
        OnPropertyChanged(nameof(NeedsSelection));
        return true;
    }

    private async Task LoadSelectedDiffAsync(CancellationToken token)
    {
        var change = _selectedChange;
        var path = WorktreePath;
        if (change is null || string.IsNullOrEmpty(path))
        {
            SelectedDiff = null;
            return;
        }

        try
        {
            var diff = await Task.Run(() => changes.GetDiffAsync(path, change, token), token);
            if (!token.IsCancellationRequested && ReferenceEquals(change, _selectedChange) && path == WorktreePath)
            {
                SelectedDiff = diff;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // Any git failure is shown to the user; the next poll tries again.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (!token.IsCancellationRequested && ReferenceEquals(change, _selectedChange) && path == WorktreePath)
            {
                ErrorMessage = ex.Message;
            }
        }
    }

    private IEnumerable<string> DistinctPaths()
        => _last.Staged.Concat(_last.Unstaged).Select(change => change.Path).Distinct(StringComparer.Ordinal);

    private void RaiseCheckedChanged()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(AllChecked));
        CommitCommand.NotifyCanExecuteChanged();
    }

    private bool CanCommit => !IsBusy && _checked.Count > 0 && !string.IsNullOrWhiteSpace(CommitMessage);

    private bool CanPush => !IsBusy && UnpushedCount > 0;

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        var selection = _last.Staged.Concat(_last.Unstaged).Where(change => _checked.Contains(change.Path)).ToList();
        var message = CommitMessage.Trim();
        var files = selection.Select(change => change.Path).Distinct(StringComparer.Ordinal).Count();
        var noun = files == 1 ? "file" : "files";

        await RunActionAsync(
            "Committing…",
            (path, token) => committer.CommitAsync(path, selection, message, token),
            () =>
            {
                CommitMessage = string.Empty;
                _checked.Clear();
                RaiseCheckedChanged();
                Rows = BuildRows(_last);
                return string.Create(CultureInfo.InvariantCulture, $"Committed {files} {noun}.");
            });
    }

    [RelayCommand(CanExecute = nameof(CanPush))]
    private Task PushAsync()
        => RunActionAsync("Pushing…", (path, token) => committer.PushAsync(path, token), () => "Pushed to origin.");

    // Git runs off the UI thread. A failure is shown and leaves the message and selection as they were, so the user can retry.
    private async Task RunActionAsync(string busyText, Func<string, CancellationToken, Task> action, Func<string> onSuccess)
    {
        var path = WorktreePath;
        if (_disposed || string.IsNullOrEmpty(path))
        {
            return;
        }

        IsBusy = true;
        BusyText = busyText;
        ActionError = null;
        StatusMessage = null;
        var token = _lifetime.Token;

        try
        {
            await Task.Run(() => action(path, token), token);
            if (WorktreePath == path)
            {
                StatusMessage = onSuccess();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // Any git failure is shown to the user.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (WorktreePath == path)
            {
                ActionError = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }

        await ReloadAsync();
    }

    private void SetChecked(GitChangeRow row, bool value)
    {
        if (row.Change is not { } change)
        {
            return;
        }

        if (value ? _checked.Add(change.Path) : _checked.Remove(change.Path))
        {
            // The same file can be listed as both staged and changed; keep both boxes in step.
            foreach (var other in Rows)
            {
                if (!ReferenceEquals(other, row) && other.Change?.Path == change.Path)
                {
                    other.SetCheckedFromModel(value);
                }
            }

            RaiseCheckedChanged();
            CommitCommand.NotifyCanExecuteChanged();
        }
    }

    private List<GitChangeRow> BuildRows(WorkingTreeChanges result)
    {
        var rows = new List<GitChangeRow>();
        AddGroup(rows, "Staged", result.Staged);
        AddGroup(rows, "Changes", result.Unstaged);
        return rows;
    }

    private void AddGroup(List<GitChangeRow> rows, string title, IReadOnlyList<FileChange> group)
    {
        if (group.Count == 0)
        {
            return;
        }

        rows.Add(new GitChangeRow(string.Create(CultureInfo.InvariantCulture, $"{title} ({group.Count})"), null));
        rows.AddRange(group.Select(change => new GitChangeRow(change.Path, change)
        {
            InitiallyChecked = _checked.Contains(change.Path),
            Toggled = SetChecked,
        }));
    }

    private static string BuildSummary(WorkingTreeChanges result)
    {
        var all = result.Staged.Concat(result.Unstaged).ToList();
        var files = all.Select(change => change.Path).Distinct(StringComparer.Ordinal).Count();
        if (files == 0)
        {
            return string.Empty;
        }

        var additions = all.Sum(change => change.Additions ?? 0);
        var deletions = all.Sum(change => change.Deletions ?? 0);
        var noun = files == 1 ? "file" : "files";
        return string.Create(CultureInfo.InvariantCulture, $"{files} {noun} changed  +{additions}  −{deletions}");
    }
}
