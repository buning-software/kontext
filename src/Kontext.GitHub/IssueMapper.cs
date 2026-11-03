using Kontext.Domain;
using Kontext.Kernel;
using Octokit;

namespace Kontext.GitHub;

internal static class IssueMapper
{
    public static GitHubIssue? Map(Issue issue, RepositoryRef? requestedRepository, string host = GitHubHosts.Default)
    {
        if (issue.PullRequest is not null)
        {
            return null;
        }

        var repository = issue.Repository is { } source
            ? new RepositoryRef(source.Owner.Login, source.Name, Host: host)
            : requestedRepository ?? RepositoryFromIssueUrl(issue.HtmlUrl, host);

        return new GitHubIssue(
            repository,
            issue.Number,
            issue.Title,
            issue.Body,
            issue.State.StringValue,
            (issue.Labels ?? []).Select(label => label.Name).ToList(),
            new Uri(issue.HtmlUrl),
            issue.User?.Login,
            issue.UpdatedAt ?? issue.CreatedAt);
    }

    private static RepositoryRef RepositoryFromIssueUrl(string htmlUrl, string host)
    {
        var segments = new Uri(htmlUrl).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return new RepositoryRef(segments[0], segments[1], Host: host);
    }
}
