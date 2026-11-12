using Kontext.Domain;

namespace Kontext.Contracts;

/// <summary>The branches GitHub lists under an issue's Development section.</summary>
public interface IIssueBranchLinks
{
    Task<bool> IsLinkedAsync(GitHubIssue issue, string branchName, CancellationToken cancellationToken = default);

    /// <summary>Creates the branch on GitHub at the given commit, linked to the issue. GitHub must not have a branch of that name yet.</summary>
    Task LinkAsync(GitHubIssue issue, string branchName, string commitOid, CancellationToken cancellationToken = default);
}
