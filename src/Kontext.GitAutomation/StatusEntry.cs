namespace Kontext.GitAutomation;

/// <summary>One record of <c>git status --porcelain=v2 -z</c>. <see cref="Index"/> and <see cref="Worktree"/> are the XY characters; '.' means unchanged.</summary>
internal sealed record StatusEntry(string Path, string? OriginalPath, char Index, char Worktree, bool IsUntracked, bool IsConflicted);
