using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

/// <summary>
/// Clears the session of closed issues in the background, the same way the Clear session button does, but without asking.
/// Because nobody confirms it, a worktree that still holds work of its own (uncommitted changes or commits no remote has) is left alone,
/// and so is the issue the user is looking at. A clear that fails leaves the issue as Failed for the user to clear by hand.
/// </summary>
public sealed class AutoCleanup(IIssueSessionClearer clearer, IWorkspaceChanges changes, IWorkspaceCommitter committer, IAutoCleanupSetting setting)
{
    private readonly HashSet<IssueKey> _busy = [];

    public async Task RunAsync(IReadOnlyList<IssueItemViewModel> items, IssueKey? selected)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (!setting.IsEnabled)
        {
            return;
        }

        foreach (var item in items.Where(item => IsClosedAndDone(item) && item.Key != selected).ToList())
        {
            if (!_busy.Add(item.Key))
            {
                continue;
            }

            try
            {
                if (!await HoldsLocalWorkAsync(item.State.WorktreePath!))
                {
                    await clearer.ClearAsync(item.Issue);
                }
            }
            catch (KontextException)
            {
            }
            finally
            {
                _busy.Remove(item.Key);
            }
        }
    }

    private static bool IsClosedAndDone(IssueItemViewModel item)
        => item.Status == IssueWorkStatus.Done && item.Issue.IsClosed && !string.IsNullOrEmpty(item.State.WorktreePath);

    private async Task<bool> HoldsLocalWorkAsync(string worktreePath)
    {
        var current = await changes.GetChangesAsync(worktreePath);
        if (current.Staged.Count > 0 || current.Unstaged.Count > 0)
        {
            return true;
        }

        return await committer.CountUnpushedAsync(worktreePath) > 0;
    }
}
