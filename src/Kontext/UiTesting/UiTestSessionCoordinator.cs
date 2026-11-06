using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.UiTesting;

/// <summary>Never clones, starts Claude or opens a terminal: UI tests must not touch the machine.</summary>
internal sealed class UiTestSessionCoordinator(IIssueWorkTracker tracker) : IIssueSessionCoordinator
{
    public Task StartOrResumeAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        // One issue pretends to have a worktree, so its Git tab is available.
        if (issue.Key == UiTestWorkspace.IssueWithWorktree)
        {
            tracker.Report(issue.Key, new IssueWorkState(IssueWorkStatus.Ready, "features/12-draw-the-coastline", UiTestWorkspace.WorktreePath));
        }

        return Task.CompletedTask;
    }
}
