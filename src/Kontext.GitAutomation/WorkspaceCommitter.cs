using System.Globalization;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.GitAutomation;

internal sealed class WorkspaceCommitter(IGitProcess git) : IWorkspaceCommitter
{
    private const string Literal = "--literal-pathspecs";

    public async Task CommitAsync(string worktreePath, IReadOnlyList<FileChange> changes, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var paths = changes
            .SelectMany(change => change.OriginalPath is { } original ? new[] { change.Path, original } : [change.Path])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (paths.Count == 0)
        {
            throw new GitAutomationException("Nothing to commit: no files are selected.", 0, string.Empty);
        }

        // Only the current paths are added: a rename's original path is already gone from the index and the worktree,
        // and so is a staged deletion, which git add rejects as a pathspec that matches nothing. Both are committed as they are.
        var added = changes
            .Where(change => change is not { Status: FileChangeStatus.Deleted, IsStaged: true })
            .Select(change => change.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (added.Count > 0)
        {
            await git.RunCheckedAsync(worktreePath, [Literal, "add", "-A", "--", .. added], cancellationToken);
        }

        // Exit code 0 means the selected paths have nothing staged, so a commit would fail or be empty.
        var staged = await git.RunAsync(worktreePath, [Literal, "diff", "--cached", "--quiet", "--", .. paths], cancellationToken);
        if (staged.Succeeded)
        {
            throw new GitAutomationException("Nothing to commit: the selected files have no changes.", 0, string.Empty);
        }

        // A pathspec makes git commit only those paths, so other staged files stay staged and out of this commit.
        await git.RunCheckedAsync(worktreePath, [Literal, "commit", "-m", message, "--", .. paths], cancellationToken);
    }

    public async Task PushAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        var result = await git.RunAsync(worktreePath, ["push", "--set-upstream", "origin", "HEAD"], cancellationToken);
        if (!result.Succeeded)
        {
            throw GitProcessExtensions.Failure(result);
        }
    }

    public async Task<int> CountUnpushedAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        // Counts commits reachable from HEAD that no remote-tracking branch has; it works before an upstream exists.
        var result = await git.RunAsync(worktreePath, ["--no-optional-locks", "rev-list", "--count", "HEAD", "--not", "--remotes"], cancellationToken);
        return result.Succeeded && int.TryParse(result.Output, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;
    }
}
