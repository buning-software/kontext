namespace Kontext.Domain;

/// <summary>The diff of one file. <paramref name="Notice"/> explains a missing or cut-off diff (binary, too large, conflict).</summary>
public sealed record FileDiff(string Path, IReadOnlyList<DiffLine> Lines, string? Notice = null)
{
    public bool Equals(FileDiff? other)
        => other is not null && Path == other.Path && Notice == other.Notice && Lines.SequenceEqual(other.Lines);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Path);
        hash.Add(Notice);
        foreach (var line in Lines)
        {
            hash.Add(line);
        }

        return hash.ToHashCode();
    }
}
