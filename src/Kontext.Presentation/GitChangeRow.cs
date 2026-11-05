using System.Globalization;
using Kontext.Domain;

namespace Kontext.Presentation;

/// <summary>One line of the change list: either a group header ("Staged (2)") or a changed file.</summary>
public sealed record GitChangeRow(string Text, FileChange? Change)
{
    public bool IsHeader => Change is null;

    public string StatusLetter => Change?.Status switch
    {
        FileChangeStatus.Modified => "M",
        // A new file is "+", staged or not.
        FileChangeStatus.Added or FileChangeStatus.Untracked => "+",
        FileChangeStatus.Deleted => "D",
        FileChangeStatus.Renamed => "R",
        FileChangeStatus.Conflicted => "!",
        _ => string.Empty,
    };

    public string FileName => Change is null ? string.Empty : Change.Path[(Change.Path.LastIndexOf('/') + 1)..];

    public string Folder => Change is null ? string.Empty : Change.Path[..Math.Max(Change.Path.LastIndexOf('/'), 0)];

    public bool HasCounts => Change is { Additions: not null } or { Deletions: not null };

    public string AdditionsText => Change?.Additions is { } additions ? string.Create(CultureInfo.InvariantCulture, $"+{additions}") : string.Empty;

    public string DeletionsText => Change?.Deletions is { } deletions ? string.Create(CultureInfo.InvariantCulture, $"−{deletions}") : string.Empty;
}
