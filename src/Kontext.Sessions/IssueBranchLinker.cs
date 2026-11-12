using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

/// <summary>
/// Links a worktree's branch to its issue the first time it is needed. Once a branch is linked, or found to be on GitHub already
/// without a link (which cannot be fixed without deleting it), it is remembered so later commits do not ask GitHub again.
/// </summary>
internal sealed class IssueBranchLinker(IIssueBranchLinks links, IWorkspaceBranches branches) : IIssueBranchLinker
{
    private readonly Lock _gate = new();
    private readonly HashSet<(IssueKey Issue, string Branch)> _settled = [];

    public async Task LinkAsync(GitHubIssue issue, string worktreePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        var branch = await branches.GetBranchAsync(worktreePath, cancellationToken);
        var settled = (issue.Key, branch);
        lock (_gate)
        {
            if (_settled.Contains(settled))
            {
                return;
            }
        }

        if (!await links.IsLinkedAsync(issue, branch, cancellationToken)
            && await branches.FindUnpublishedBaseAsync(worktreePath, branch, cancellationToken) is { } start)
        {
            await links.LinkAsync(issue, branch, start, cancellationToken);
        }

        lock (_gate)
        {
            _settled.Add(settled);
        }
    }
}
