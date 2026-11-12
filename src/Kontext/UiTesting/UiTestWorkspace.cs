using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.UiTesting;

/// <summary>
/// An in-memory worktree for the Git tab: three changed files that a commit removes from the list, and a counter of
/// unpushed commits. A commit message containing "fail" makes the commit fail; one containing "reject" makes the next
/// push fail once. Never touches git or the disk.
/// </summary>
internal sealed class UiTestWorkspace : IWorkspaceChanges, IWorkspaceCommitter
{
    public const string WorktreePath = @"C:\ui-test\worktree";

    public static IssueKey IssueWithWorktree { get; } = new("acme/atlas", 12);

    private readonly object _lock = new();
    private readonly List<FileChange> _changes =
    [
        Modified("src/Map.cs", 4, 1),
        Modified("src/Legend.cs", 2, 0),
        new("notes.txt", null, FileChangeStatus.Untracked, IsStaged: false, 1, 0, IsBinary: false),
    ];

    private int _unpushed;
    private bool _rejectNextPush;

    public Task<WorkingTreeChanges> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(new WorkingTreeChanges([], (FileChange[])[.. _changes]));
        }
    }

    public Task<FileDiff> GetDiffAsync(string worktreePath, FileChange change, CancellationToken cancellationToken = default)
        => Task.FromResult(new FileDiff(change.Path, (DiffLine[])[new DiffLine(DiffLineKind.Added, null, 1, $"changed {change.Path}")]));

    public Task CommitAsync(string worktreePath, IReadOnlyList<FileChange> changes, string message, CancellationToken cancellationToken = default)
    {
        if (message.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromException(new InvalidOperationException("Commit failed (simulated)."));
        }

        lock (_lock)
        {
            _changes.RemoveAll(existing => changes.Any(committed => committed.Path == existing.Path));
            _unpushed++;
            _rejectNextPush = message.Contains("reject", StringComparison.OrdinalIgnoreCase);
        }

        return Task.CompletedTask;
    }

    public Task PushAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_rejectNextPush)
            {
                _rejectNextPush = false;
                return Task.FromException(new InvalidOperationException("Push rejected (simulated)."));
            }

            _unpushed = 0;
        }

        return Task.CompletedTask;
    }

    public Task<int> CountUnpushedAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_unpushed);
        }
    }

    private static FileChange Modified(string path, int additions, int deletions)
        => new(path, null, FileChangeStatus.Modified, IsStaged: false, additions, deletions, IsBinary: false);
}
