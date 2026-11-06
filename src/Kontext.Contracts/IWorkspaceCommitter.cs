using Kontext.Domain;

namespace Kontext.Contracts;

/// <summary>Commits and pushes the changes of one worktree.</summary>
public interface IWorkspaceCommitter
{
    /// <summary>Commits exactly the given changes with the message; other staged or unstaged files are left alone.</summary>
    Task CommitAsync(string worktreePath, IReadOnlyList<FileChange> changes, string message, CancellationToken cancellationToken = default);

    /// <summary>Pushes the checked-out branch to <c>origin</c>, setting it as the upstream.</summary>
    Task PushAsync(string worktreePath, CancellationToken cancellationToken = default);

    /// <summary>The number of commits on the checked-out branch that no remote has yet.</summary>
    Task<int> CountUnpushedAsync(string worktreePath, CancellationToken cancellationToken = default);
}
