using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Testing;

public sealed class FakeIssueBranchLinks : IIssueBranchLinks
{
    public HashSet<string> Linked { get; } = [];

    public List<(int Issue, string Branch, string Commit)> Created { get; } = [];

    public int Lookups { get; private set; }

    public Exception? FailWith { get; set; }

    public Task<bool> IsLinkedAsync(GitHubIssue issue, string branchName, CancellationToken cancellationToken = default)
    {
        Lookups++;
        return FailWith is null ? Task.FromResult(Linked.Contains(branchName)) : Task.FromException<bool>(FailWith);
    }

    public Task LinkAsync(GitHubIssue issue, string branchName, string commitOid, CancellationToken cancellationToken = default)
    {
        Created.Add((issue.Number, branchName, commitOid));
        Linked.Add(branchName);
        return Task.CompletedTask;
    }
}
