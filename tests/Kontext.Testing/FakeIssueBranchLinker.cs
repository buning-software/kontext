using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Testing;

public sealed class FakeIssueBranchLinker : IIssueBranchLinker
{
    private readonly object _lock = new();
    private readonly List<(GitHubIssue Issue, string WorktreePath)> _links = [];

    public Exception? FailWith { get; set; }

    public IReadOnlyList<(GitHubIssue Issue, string WorktreePath)> Links
    {
        get
        {
            lock (_lock)
            {
                return [.. _links];
            }
        }
    }

    public Task LinkAsync(GitHubIssue issue, string worktreePath, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _links.Add((issue, worktreePath));
        }

        return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
    }
}
