using System.Text;
using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.GitAutomation;

internal sealed class WorkspaceChanges(IGitProcess git) : IWorkspaceChanges
{
    private const int MaxUntrackedBytes = 256 * 1024;
    private const int BinarySniffBytes = 8000;

    // Untracked files can number in the thousands (an unignored build folder); only the first ones are read for line counts.
    private const int MaxCountedUntrackedFiles = 200;

    // A diff this big is never worth reading into memory on every poll.
    private const int MaxDiffChangedLines = 20_000;

    // Polling runs while Claude may be running git itself; never take the index lock.
    private const string NoLocks = "--no-optional-locks";

    public const string ConflictNotice = "Merge conflict. Resolve it in the terminal.";

    public const string TooLargeNotice = "File too large to preview.";

    public const string UnavailableNotice = "File not available.";

    public const string DiffTooLargeNotice = "Diff too large to preview.";

    public async Task<WorkingTreeChanges> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        var status = await git.RunCheckedAsync(worktreePath, [NoLocks, "status", "--porcelain=v2", "-z", "--untracked-files=all"], cancellationToken);
        var entries = StatusPorcelainV2Parser.Parse(status.StandardOutput);
        if (entries.Count == 0)
        {
            return WorkingTreeChanges.Empty;
        }

        var stagedCounts = await CountsAsync(worktreePath, staged: true, needed: entries.Any(entry => IsTrackedChange(entry, entry.Index)), cancellationToken);
        var unstagedCounts = await CountsAsync(worktreePath, staged: false, needed: entries.Any(entry => IsTrackedChange(entry, entry.Worktree)), cancellationToken);

        var staged = new List<FileChange>();
        var unstaged = new List<FileChange>();
        var untrackedSeen = 0;

        foreach (var entry in entries)
        {
            if (entry.IsConflicted)
            {
                unstaged.Add(new FileChange(entry.Path, null, FileChangeStatus.Conflicted, IsStaged: false, null, null, IsBinary: false));
            }
            else if (entry.IsUntracked)
            {
                unstaged.Add(await UntrackedChangeAsync(worktreePath, entry.Path, countLines: untrackedSeen++ < MaxCountedUntrackedFiles, cancellationToken));
            }
            else
            {
                if (entry.Index != '.')
                {
                    staged.Add(TrackedChange(entry, entry.Index, isStaged: true, stagedCounts));
                }

                if (entry.Worktree != '.')
                {
                    unstaged.Add(TrackedChange(entry, entry.Worktree, isStaged: false, unstagedCounts));
                }
            }
        }

        return new WorkingTreeChanges(Sorted(staged), Sorted(unstaged));
    }

    public async Task<FileDiff> GetDiffAsync(string worktreePath, FileChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        switch (change.Status)
        {
            case FileChangeStatus.Untracked:
                return await UntrackedDiffAsync(worktreePath, change.Path, cancellationToken);
            case FileChangeStatus.Conflicted:
                return new FileDiff(change.Path, [], ConflictNotice);
        }

        if ((change.Additions ?? 0) + (change.Deletions ?? 0) > MaxDiffChangedLines)
        {
            return new FileDiff(change.Path, [], DiffTooLargeNotice);
        }

        var arguments = new List<string> { NoLocks, "diff" };
        if (change.IsStaged)
        {
            arguments.Add("--cached");
        }

        arguments.AddRange(["--no-color", "--no-ext-diff", "--unified=3", "--"]);
        if (change.IsStaged && change.OriginalPath is not null)
        {
            arguments.Add(change.OriginalPath);
        }

        arguments.Add(change.Path);

        var result = await git.RunCheckedAsync(worktreePath, arguments, cancellationToken);
        return UnifiedDiffParser.Parse(change.Path, result.StandardOutput);
    }

    private static bool IsTrackedChange(StatusEntry entry, char code)
        => !entry.IsUntracked && !entry.IsConflicted && code != '.';

    private static List<FileChange> Sorted(List<FileChange> changes)
    {
        changes.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path));
        return changes;
    }

    private async Task<IReadOnlyDictionary<string, NumstatEntry>> CountsAsync(string worktreePath, bool staged, bool needed, CancellationToken cancellationToken)
    {
        if (!needed)
        {
            return new Dictionary<string, NumstatEntry>();
        }

        string[] arguments = staged
            ? [NoLocks, "diff", "--cached", "--numstat", "-z"]
            : [NoLocks, "diff", "--numstat", "-z"];

        var result = await git.RunCheckedAsync(worktreePath, arguments, cancellationToken);
        return NumstatParser.Parse(result.StandardOutput);
    }

    private static FileChange TrackedChange(StatusEntry entry, char code, bool isStaged, IReadOnlyDictionary<string, NumstatEntry> counts)
    {
        var status = code switch
        {
            'A' or 'C' => FileChangeStatus.Added,
            'D' => FileChangeStatus.Deleted,
            'R' => FileChangeStatus.Renamed,
            _ => FileChangeStatus.Modified,
        };

        var original = isStaged && status == FileChangeStatus.Renamed ? entry.OriginalPath : null;

        if (!counts.TryGetValue(entry.Path, out var count))
        {
            return new FileChange(entry.Path, original, status, isStaged, null, null, IsBinary: false);
        }

        var binary = count.Additions is null && count.Deletions is null;
        return new FileChange(entry.Path, original, status, isStaged, count.Additions, count.Deletions, binary);
    }

    private static string FullPath(string worktreePath, string path)
        => Path.Combine(worktreePath, path.Replace('/', Path.DirectorySeparatorChar));

    private static bool LooksBinary(byte[] content)
        => Array.IndexOf(content, (byte)0, 0, Math.Min(content.Length, BinarySniffBytes)) >= 0;

    /// <summary>Reads a file that another process may be writing to. Returns null when it cannot be read (locked, vanished, no access).</summary>
    private static async Task<byte[]?> TryReadAsync(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<FileChange> UntrackedChangeAsync(string worktreePath, string path, bool countLines, CancellationToken cancellationToken)
    {
        var file = new FileInfo(FullPath(worktreePath, path));
        if (!countLines || !file.Exists || file.Length > MaxUntrackedBytes)
        {
            return new FileChange(path, null, FileChangeStatus.Untracked, IsStaged: false, null, null, IsBinary: false);
        }

        var content = await TryReadAsync(file.FullName, cancellationToken);
        if (content is null)
        {
            return new FileChange(path, null, FileChangeStatus.Untracked, IsStaged: false, null, null, IsBinary: false);
        }

        if (LooksBinary(content))
        {
            return new FileChange(path, null, FileChangeStatus.Untracked, IsStaged: false, null, null, IsBinary: true);
        }

        return new FileChange(path, null, FileChangeStatus.Untracked, IsStaged: false, LineCount(content), 0, IsBinary: false);
    }

    private static int LineCount(byte[] content)
    {
        if (content.Length == 0)
        {
            return 0;
        }

        var lines = content.Count(value => value == (byte)'\n');
        return content[^1] == (byte)'\n' ? lines : lines + 1;
    }

    private static async Task<FileDiff> UntrackedDiffAsync(string worktreePath, string path, CancellationToken cancellationToken)
    {
        var file = new FileInfo(FullPath(worktreePath, path));
        if (!file.Exists)
        {
            return new FileDiff(path, [], UnavailableNotice);
        }

        if (file.Length > MaxUntrackedBytes)
        {
            return new FileDiff(path, [], TooLargeNotice);
        }

        var content = await TryReadAsync(file.FullName, cancellationToken);
        if (content is null)
        {
            return new FileDiff(path, [], UnavailableNotice);
        }

        if (LooksBinary(content))
        {
            return new FileDiff(path, [], UnifiedDiffParser.BinaryNotice);
        }

        var text = Encoding.UTF8.GetString(content);
        var lines = text.Split('\n').Select(line => line.EndsWith('\r') ? line[..^1] : line).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return new FileDiff(path, [], UnifiedDiffParser.NoChangesNotice);
        }

        var capped = lines.Take(UnifiedDiffParser.MaxLines).Select((line, index) => new DiffLine(DiffLineKind.Added, null, index + 1, line)).ToList();
        var notice = lines.Count > UnifiedDiffParser.MaxLines
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Showing the first {UnifiedDiffParser.MaxLines:N0} lines.")
            : null;

        return new FileDiff(path, capped, notice);
    }
}
