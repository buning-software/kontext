using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public interface IGitHubService
{
    Task<GitHubUser> AuthenticateAsync(string host, string accessToken, CancellationToken cancellationToken = default);

    void SignOut(string host);

    Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default);

    Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default);

    /// <summary>The newest pull request whose head is <paramref name="branchName"/> in <paramref name="repository"/>, with its workflow result, or null when there is none.</summary>
    Task<PullRequestStatus?> GetPullRequestStatusAsync(RepositoryRef repository, string branchName, CancellationToken cancellationToken = default);
}
