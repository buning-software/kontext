using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.TestSupport;

public sealed class FakeWorkspaceChanges : IWorkspaceChanges
{
    private readonly object _lock = new();
    private readonly List<string> _changeRequests = [];
    private readonly List<FileChange> _diffRequests = [];
    private readonly List<int> _threadIds = [];

    public WorkingTreeChanges Changes { get; set; } = WorkingTreeChanges.Empty;

    public Dictionary<(string Path, bool IsStaged), FileDiff> Diffs { get; } = [];

    public Exception? FailWith { get; set; }

    /// <summary>When set, <see cref="GetChangesAsync"/> takes its answer first and then waits for the gate. The wait ignores cancellation, like a git process that cannot be stopped in time.</summary>
    public TaskCompletionSource<bool>? Gate { get; set; }

    public IReadOnlyList<string> ChangeRequests
    {
        get
        {
            lock (_lock)
            {
                return [.. _changeRequests];
            }
        }
    }

    public IReadOnlyList<FileChange> DiffRequests
    {
        get
        {
            lock (_lock)
            {
                return [.. _diffRequests];
            }
        }
    }

    /// <summary>The managed thread id each call (changes or diff) ran on when it started.</summary>
    public IReadOnlyList<int> ThreadIds
    {
        get
        {
            lock (_lock)
            {
                return [.. _threadIds];
            }
        }
    }

    public async Task<WorkingTreeChanges> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _changeRequests.Add(worktreePath);
            _threadIds.Add(Environment.CurrentManagedThreadId);
        }

        var answer = Changes;
        var failure = FailWith;
        var gate = Gate;

        if (gate is not null)
        {
            await gate.Task;
        }

        return failure is null ? answer : throw failure;
    }

    public Task<FileDiff> GetDiffAsync(string worktreePath, FileChange change, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _diffRequests.Add(change);
            _threadIds.Add(Environment.CurrentManagedThreadId);
        }

        return FailWith is { } failure
            ? Task.FromException<FileDiff>(failure)
            : Task.FromResult(Diffs.TryGetValue((change.Path, change.IsStaged), out var diff) ? diff : new FileDiff(change.Path, []));
    }
}
