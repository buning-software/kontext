using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.UiTesting;

internal sealed class UiTestGitHubService : IGitHubService
{
    public Task<GitHubUser> AuthenticateAsync(string host, string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(UiTestGitHubData.User with { Host = host });

    public void SignOut(string host)
    {
    }

    public Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GitHubIssue>>(
            repository is null ? UiTestGitHubData.Issues : (GitHubIssue[])[.. UiTestGitHubData.Issues.Where(issue => Same(issue.Repository, repository))]);

    public Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default) =>
        UiTestGitHubData.Issues.FirstOrDefault(issue => Same(issue.Repository, repository) && issue.Number == number) is { } issue
            ? Task.FromResult(issue)
            : Task.FromException<GitHubIssue>(new InvalidOperationException($"Issue {repository.DisplayName}#{number} is not in the UI test data."));

    private static bool Same(RepositoryRef left, RepositoryRef right) =>
        string.Equals(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
}
