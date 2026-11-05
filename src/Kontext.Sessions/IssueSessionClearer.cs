using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

internal sealed class IssueSessionClearer(
    IIssueWorkTracker tracker,
    ISessionStore sessions,
    ITerminalSessionService terminals,
    IIssueSessionRegistry registry,
    IWorkspaceRemover workspaces,
    TimeProvider time) : IIssueSessionClearer
{
    private const int RemoveAttempts = 3;
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public async Task ClearAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var key = issue.Key;
        var state = tracker.StateOf(key);
        var stored = sessions.Find(key);
        var worktreePath = state.WorktreePath ?? stored?.WorktreePath;
        var branchName = state.BranchName ?? stored?.BranchName;

        var exited = await EndSessionAsync(key);

        if (worktreePath is not null && branchName is not null)
        {
            var failure = await RemoveWorkspaceAsync(issue, worktreePath, branchName, cancellationToken);
            if (failure is not null)
            {
                var message = exited ? failure.Message : $"The terminal did not close in time. {failure.Message}";
                tracker.Report(key, new IssueWorkState(IssueWorkStatus.Failed, branchName, worktreePath, message, SessionEnded: true));
                return;
            }
        }

        sessions.Remove(key);
        tracker.Report(key, IssueWorkState.NotStarted);
    }

    private async Task<KontextException?> RemoveWorkspaceAsync(GitHubIssue issue, string worktreePath, string branchName, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await workspaces.RemoveAsync(issue.Repository, worktreePath, branchName, cancellationToken);
                return null;
            }
            catch (KontextException ex) when (attempt >= RemoveAttempts)
            {
                return ex;
            }
            catch (KontextException)
            {
                await Task.Delay(RetryDelay, time, cancellationToken);
            }
        }
    }

    private async Task<bool> EndSessionAsync(IssueKey key)
    {
        if (registry.Find(key) is not { } session)
        {
            return true;
        }

        terminals.Close(session.Id);

        var exited = true;
        try
        {
            await session.Exited.WaitAsync(ExitTimeout, time);
        }
        catch (TimeoutException)
        {
            exited = false;
        }

        registry.Remove(key);
        return exited;
    }
}
