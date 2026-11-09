using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

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
