using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Testing;

public sealed class FakeWorkspaceCommitter : IWorkspaceCommitter
{
    private readonly object _lock = new();
    private readonly List<CommitRequest> _commits = [];
    private readonly List<string> _pushes = [];

    public int Unpushed { get; set; }

    public Exception? CommitFailure { get; set; }

    public Exception? PushFailure { get; set; }

    /// <summary>When set, a commit or push waits for the gate after recording the call.</summary>
    public TaskCompletionSource<bool>? Gate { get; set; }

    /// <summary>Runs after a successful commit, so a test can change what the fake workspace reports.</summary>
    public Action<CommitRequest>? OnCommitted { get; set; }

    public IReadOnlyList<CommitRequest> Commits
    {
        get
        {
            lock (_lock)
            {
                return [.. _commits];
            }
        }
    }

    public IReadOnlyList<string> Pushes
    {
        get
        {
            lock (_lock)
            {
                return [.. _pushes];
            }
        }
    }

    public async Task CommitAsync(string worktreePath, IReadOnlyList<FileChange> changes, string message, CancellationToken cancellationToken = default)
    {
        var request = new CommitRequest(worktreePath, changes, message);
        lock (_lock)
        {
            _commits.Add(request);
        }

        if (Gate is { } gate)
        {
            await gate.Task;
        }

        if (CommitFailure is { } failure)
        {
            throw failure;
        }

        Unpushed++;
        OnCommitted?.Invoke(request);
    }

    public async Task PushAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pushes.Add(worktreePath);
        }

        if (Gate is { } gate)
        {
            await gate.Task;
        }

        if (PushFailure is { } failure)
        {
            throw failure;
        }

        Unpushed = 0;
    }

    public Task<int> CountUnpushedAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(Unpushed);
}
