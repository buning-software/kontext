using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.TestSupport;

public static class Issues
{
    public static GitHubIssue Create(
        int number = 1,
        string title = "A title",
        string repository = "acme/app",
        string[]? labels = null,
        string? author = "octocat",
        DateTimeOffset? updatedAt = null,
        string? body = "A body")
    {
        var parts = repository.Split('/');
        var repo = new RepositoryRef(parts[0], parts[1]);
        return new GitHubIssue(
            repo,
            number,
            title,
            body,
            "open",
            labels ?? [],
            new Uri($"https://github.com/{repository}/issues/{number}"),
            author,
            updatedAt ?? DateTimeOffset.Parse("2025-03-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed class FakeGitHubService : IGitHubService
{
    private readonly List<TaskCompletionSource<IReadOnlyList<GitHubIssue>>> _pending = [];

    public IReadOnlyList<GitHubIssue> Issues { get; set; } = [];

    public List<GitHubIssue> ClosedIssues { get; } = [];

    public Exception? FailWith { get; set; }

    public bool Deferred { get; set; }

    public int Calls { get; private set; }

    public void Complete(int callIndex, IReadOnlyList<GitHubIssue> issues) => _pending[callIndex].SetResult(issues);

    public Task<IReadOnlyList<GitHubIssue>> GetIssuesAsync(RepositoryRef? repository = null, CancellationToken cancellationToken = default)
    {
        Calls++;

        if (FailWith is not null)
        {
            return Task.FromException<IReadOnlyList<GitHubIssue>>(FailWith);
        }

        if (!Deferred)
        {
            return Task.FromResult(Issues);
        }

        var source = new TaskCompletionSource<IReadOnlyList<GitHubIssue>>();
        _pending.Add(source);
        return source.Task;
    }

    public GitHubUser SignedInAs { get; set; } = new("octocat", "The Octocat", new Uri("https://github.com/octocat"));

    public GitHubUser EnterpriseSignedInAs { get; set; } = new("corpuser", "Corp User", new Uri("https://github.corp.com/corpuser"), "github.corp.com");

    public List<string> HostsTried { get; } = [];

    public List<string> SignedOutHosts { get; } = [];

    public Exception? AuthenticationFailsWith { get; set; }

    public List<string> TokensTried { get; } = [];

    public int SignOuts { get; private set; }

    public Action<string>? OnAuthenticate { get; set; }

    public Task<GitHubUser> AuthenticateAsync(string host, string accessToken, CancellationToken cancellationToken = default)
    {
        HostsTried.Add(host);
        TokensTried.Add(accessToken);
        OnAuthenticate?.Invoke(accessToken);
        var user = GitHubHosts.IsDefault(host) ? SignedInAs : EnterpriseSignedInAs with { Host = host };
        return AuthenticationFailsWith is null ? Task.FromResult(user) : Task.FromException<GitHubUser>(AuthenticationFailsWith);
    }

    public void SignOut(string host)
    {
        SignOuts++;
        SignedOutHosts.Add(host);
    }


    public Task<GitHubIssue> GetIssueAsync(RepositoryRef repository, int number, CancellationToken cancellationToken = default)
    {
        var found = ClosedIssues.Concat(Issues).FirstOrDefault(issue => issue.Number == number && string.Equals(issue.Repository.FullName, repository.FullName, StringComparison.OrdinalIgnoreCase));
        return found is null ? Task.FromException<GitHubIssue>(new TestKontextException("not found")) : Task.FromResult(found);
    }
}

public sealed class FakeTokenStore(string? saved = null, string? savedEnterprise = null, string enterpriseHost = "github.corp.com") : IGitHubTokenStore
{
    public string? Saved { get; private set; } = saved;

    public string? SavedEnterprise { get; private set; } = savedEnterprise;

    public string? EnterpriseHost { get; private set; } = savedEnterprise is null ? null : enterpriseHost;

    public int Clears { get; private set; }

    public int EnterpriseClears { get; private set; }

    public Exception? LoadFailsWith { get; set; }

    public string? Load(string host)
    {
        if (LoadFailsWith is not null)
        {
            throw LoadFailsWith;
        }

        return GitHubHosts.IsDefault(host) ? Saved : string.Equals(host, EnterpriseHost, StringComparison.OrdinalIgnoreCase) ? SavedEnterprise : null;
    }

    public void Save(string host, string token)
    {
        if (GitHubHosts.IsDefault(host))
        {
            Saved = token;
            return;
        }

        EnterpriseHost = host;
        SavedEnterprise = token;
    }

    public void Clear(string host)
    {
        if (GitHubHosts.IsDefault(host))
        {
            Saved = null;
            Clears++;
            return;
        }

        SavedEnterprise = null;
        EnterpriseHost = null;
        EnterpriseClears++;
    }

    public string? FindEnterpriseHost() => EnterpriseHost;
}

public sealed class FakeEnterprisePrompt : IEnterpriseServerPrompt
{
    public EnterpriseCredentials? Answer { get; set; } = new(new GitHubServer("github.corp.com"), "ghp_corporate");

    public int Asked { get; private set; }

    public Task<EnterpriseCredentials?> AskAsync()
    {
        Asked++;
        return Task.FromResult(Answer);
    }
}
