using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Presentation;

/// <summary>
/// The uncommitted changes of one worktree. It reloads when it becomes active and then polls, but only while
/// <see cref="IsActive"/> and a <see cref="WorktreePath"/> are both set. Await continuations return to the
/// thread that set those properties (the UI thread), so no dispatcher is needed.
/// </summary>
public sealed partial class GitChangesViewModel(IWorkspaceChanges changes, TimeProvider clock, GitChangesOptions options) : ObservableObject, IDisposable
{
    private readonly LatestOnly _reloads = new();
    private readonly LatestOnly _selections = new();
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
        _disposed = true;
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
            if (!IsCurrent(operation, path))
            {
                return;
            }

            Apply(result);
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
                await ReloadAsync();
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
            Rows = BuildRows(result);
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

    private static List<GitChangeRow> BuildRows(WorkingTreeChanges result)
    {
        var rows = new List<GitChangeRow>();
        AddGroup(rows, "Staged", result.Staged);
        AddGroup(rows, "Changes", result.Unstaged);
        return rows;
    }

    private static void AddGroup(List<GitChangeRow> rows, string title, IReadOnlyList<FileChange> group)
    {
        if (group.Count == 0)
        {
            return;
        }

        rows.Add(new GitChangeRow(string.Create(CultureInfo.InvariantCulture, $"{title} ({group.Count})"), null));
        rows.AddRange(group.Select(change => new GitChangeRow(change.Path, change)));
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
