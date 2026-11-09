using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeCoordinator : IIssueSessionCoordinator
{
    public List<GitHubIssue> Started { get; } = [];

    public Task StartOrResumeAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        Started.Add(issue);
        return Task.CompletedTask;
    }
}
