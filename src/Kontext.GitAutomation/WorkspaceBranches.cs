using Kontext.Contracts;

namespace Kontext.GitAutomation;

internal sealed class WorkspaceBranches(IGitProcess git) : IWorkspaceBranches
{
    private const string RemoteDefaultBranch = "refs/remotes/origin/HEAD";

    public async Task<string> GetBranchAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        return (await git.RunCheckedAsync(worktreePath, ["symbolic-ref", "--short", "HEAD"], cancellationToken)).Output;
    }

    public async Task<string?> FindUnpublishedBaseAsync(string worktreePath, string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        var remote = await git.RunCheckedAsync(worktreePath, ["ls-remote", "--heads", "origin", $"refs/heads/{branchName}"], cancellationToken);
        if (remote.Output.Length > 0)
        {
            return null;
        }

        var shared = await git.RunAsync(worktreePath, ["merge-base", "HEAD", RemoteDefaultBranch], cancellationToken);
        return shared.Succeeded && shared.Output.Length > 0 ? shared.Output : null;
    }
}
