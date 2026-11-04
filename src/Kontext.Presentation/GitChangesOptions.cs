namespace Kontext.Presentation;

public sealed record GitChangesOptions(TimeSpan PollInterval)
{
    public static GitChangesOptions Default { get; } = new(TimeSpan.FromSeconds(3));
}
