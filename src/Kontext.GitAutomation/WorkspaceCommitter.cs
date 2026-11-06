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

        // Only the current paths are added: a rename's original path is already gone from the index and the worktree.
        var added = changes.Select(change => change.Path).Distinct(StringComparer.Ordinal).ToList();
        await git.RunCheckedAsync(worktreePath, [Literal, "add", "-A", "--", .. added], cancellationToken);

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
            throw new GitAutomationException(DescribePushFailure(result), result.ExitCode, result.StandardError);
        }
    }

    public async Task<int> CountUnpushedAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worktreePath);

        // Counts commits reachable from HEAD that no remote-tracking branch has; it works before an upstream exists.
        var result = await git.RunAsync(worktreePath, ["--no-optional-locks", "rev-list", "--count", "HEAD", "--not", "--remotes"], cancellationToken);
        return result.Succeeded && int.TryParse(result.Output, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : 0;
    }

    private static string DescribePushFailure(GitResult result)
    {
        var error = result.StandardError;
        if (Contains(error, "non-fast-forward") || Contains(error, "[rejected]") || Contains(error, "fetch first"))
        {
            return "Push rejected: the remote branch has commits you do not have. Pull or rebase, then push again.";
        }

        if (Contains(error, "Authentication failed") || Contains(error, "could not read Username") || Contains(error, "Permission denied")
            || Contains(error, "returned error: 403") || Contains(error, "returned error: 401"))
        {
            return "Push failed: authentication failed. Sign in to GitHub again and retry.";
        }

        var detail = string.IsNullOrWhiteSpace(error) ? result.StandardOutput.Trim() : error.Trim();
        return $"Push failed: {detail}";
    }

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);
}
