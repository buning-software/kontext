using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeSessionClearer : IIssueSessionClearer
{
    public List<GitHubIssue> Cleared { get; } = [];

    public Task ClearAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        Cleared.Add(issue);
        return Task.CompletedTask;
    }
}
