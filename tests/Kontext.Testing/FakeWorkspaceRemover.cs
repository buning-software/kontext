using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeWorkspaceRemover : IWorkspaceRemover
{
    public List<(RepositoryRef Repository, string WorktreePath, string BranchName)> Removed { get; } = [];

    public Exception? FailWith { get; set; }

    public Task RemoveAsync(RepositoryRef repository, string worktreePath, string branchName, CancellationToken cancellationToken = default)
    {
        Removed.Add((repository, worktreePath, branchName));
        return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
    }
}
