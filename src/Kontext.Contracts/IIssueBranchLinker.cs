using Kontext.Domain;

namespace Kontext.Contracts;

/// <summary>Links the branch of an issue's worktree to the issue on GitHub, so it shows under the issue's Development section.</summary>
public interface IIssueBranchLinker
{
    /// <summary>
    /// Links the branch unless it already is. Only a branch GitHub does not have yet can be linked; one that was pushed before is left as it is.
    /// </summary>
    Task LinkAsync(GitHubIssue issue, string worktreePath, CancellationToken cancellationToken = default);
}
