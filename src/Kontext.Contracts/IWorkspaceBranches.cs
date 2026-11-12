namespace Kontext.Contracts;

/// <summary>What a worktree's branch looks like from the remote's side, for linking it to its issue.</summary>
public interface IWorkspaceBranches
{
    /// <summary>The name of the branch checked out in the worktree.</summary>
    Task<string> GetBranchAsync(string worktreePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// A commit the remote already has and the branch starts from, as long as <c>origin</c> does not have the branch yet; otherwise null.
    /// A branch can only be created on GitHub at a commit GitHub knows, so this is where a linked branch can start before the first push.
    /// </summary>
    Task<string?> FindUnpublishedBaseAsync(string worktreePath, string branchName, CancellationToken cancellationToken = default);
}
