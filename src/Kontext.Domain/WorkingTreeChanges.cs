namespace Kontext.Domain;

public sealed record WorkingTreeChanges(IReadOnlyList<FileChange> Staged, IReadOnlyList<FileChange> Unstaged)
{
    public static WorkingTreeChanges Empty { get; } = new([], []);

    public bool Equals(WorkingTreeChanges? other)
        => other is not null && Staged.SequenceEqual(other.Staged) && Unstaged.SequenceEqual(other.Unstaged);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var change in Staged)
        {
            hash.Add(change);
        }

        hash.Add(int.MinValue);
        foreach (var change in Unstaged)
        {
            hash.Add(change);
        }

        return hash.ToHashCode();
    }
}
