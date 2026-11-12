using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using Octokit;

namespace Kontext.GitHub;

internal sealed class GitHubService(GitHubClients clients) : IGitHubService
{
    private const int PageSize = 100;

    private const int AllRepositoriesPageCount = 10;

    private static readonly ProductHeaderValue ProductHeader = new("Kontext");

    public async Task<GitHubUser> AuthenticateAsync(string host, string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var candidate = CreateClient(host, accessToken);

        var user = await OctokitCall.RunAsync(() => candidate.User.Current(), cancellationToken);

        clients.Set(host, candidate);
        return new GitHubUser(user.Login, user.Name, new Uri(user.HtmlUrl), host);
    }

    public void SignOut(string host) => clients.Remove(host);

    public async Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default)
    {
        if (repository is not null)
        {
            return await GetRepositoryIssuesAsync(clients.For(repository.Host), repository, cancellationToken);
        }

        var signedIn = clients.All();
        if (signedIn.Length == 0)
        {
            return [];
        }

        var attempts = await Task.WhenAll(signedIn.Select(entry => TryGetAllIssuesAsync(entry.Value, entry.Key, cancellationToken)));

        // One unreachable server (a company server that needs a VPN, say) must not hide the issues
        // of the other account, so only fail when every signed-in account failed.
        if (attempts.All(attempt => attempt.Failure is not null))
        {
            throw attempts[0].Failure!;
        }

        return attempts.SelectMany(attempt => attempt.Issues).OrderByDescending(issue => issue.UpdatedAt).ToList();
    }

    public async Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var client = clients.For(repository.Host);
        var issue = await OctokitCall.RunAsync(() => client.Issue.Get(repository.Owner, repository.Name, number), cancellationToken);

        return IssueMapper.Map(issue, repository, repository.Host)
            ?? throw new GitHubException($"{repository.FullName}#{number} is a pull request, not an issue.");
    }

    private static GitHubClient CreateClient(string host, string accessToken)
    {
        var client = GitHubHosts.IsDefault(host)
            ? new GitHubClient(ProductHeader)
            : new GitHubClient(ProductHeader, new GitHubServer(host).ApiUri);

        client.Credentials = new Credentials(accessToken);
        return client;
    }

    private static async Task<(IReadOnlyList<GitHubIssue> Issues, KontextException? Failure)> TryGetAllIssuesAsync(GitHubClient client, string host, CancellationToken cancellationToken)
    {
        try
        {
            var issues = await OctokitCall.RunAsync(
                () => client.Issue.GetAllForCurrent(
                    new IssueRequest
                    {
                        Filter = IssueFilter.All,
                        State = ItemStateFilter.Open,
                        SortProperty = IssueSort.Updated,
                        SortDirection = SortDirection.Descending,
                    },
                    new ApiOptions { PageSize = PageSize, PageCount = AllRepositoriesPageCount }),
                cancellationToken);

            return (Map(issues, null, host), null);
        }
        catch (KontextException ex)
        {
            return ([], ex);
        }
    }

    private static async Task<IReadOnlyList<GitHubIssue>> GetRepositoryIssuesAsync(GitHubClient client, RepositoryRef repository, CancellationToken cancellationToken)
    {
        var issues = await OctokitCall.RunAsync(
            () => client.Issue.GetAllForRepository(
                repository.Owner,
                repository.Name,
                new RepositoryIssueRequest
                {
                    State = ItemStateFilter.Open,
                    SortProperty = IssueSort.Updated,
                    SortDirection = SortDirection.Descending,
                },
                new ApiOptions { PageSize = PageSize }),
            cancellationToken);

        return Map(issues, repository, repository.Host);
    }

    private static List<GitHubIssue> Map(IEnumerable<Issue> issues, RepositoryRef? repository, string host) =>
        issues
            .Select(issue => IssueMapper.Map(issue, repository, host))
            .OfType<GitHubIssue>()
            .ToList();
}
