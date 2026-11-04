using Kontext.Domain;

namespace Kontext.Contracts;

public interface IWorkspaceChanges
{
    Task<WorkingTreeChanges> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default);

    Task<FileDiff> GetDiffAsync(string worktreePath, FileChange change, CancellationToken cancellationToken = default);
}
