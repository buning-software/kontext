namespace Kontext.Domain;

public enum FileChangeStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflicted,
}

/// <summary>One changed file. Paths are relative to the worktree root and use '/' as git prints them.</summary>
/// <param name="Additions">Null when unknown (binary or oversized files).</param>
/// <param name="Deletions">Null when unknown (binary or oversized files).</param>
public sealed record FileChange(
    string Path,
    string? OriginalPath,
    FileChangeStatus Status,
    bool IsStaged,
    int? Additions,
    int? Deletions,
    bool IsBinary);
