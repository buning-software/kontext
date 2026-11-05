namespace Kontext.Presentation;

public sealed record RefreshOptions(TimeSpan Interval)
{
    public static RefreshOptions Default { get; } = new(TimeSpan.FromSeconds(60));

    public bool IsEnabled => Interval > TimeSpan.Zero;
}
