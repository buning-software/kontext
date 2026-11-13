namespace Kontext.Presentation;

public sealed record PullRequestStatusOptions(TimeSpan PollInterval)
{
    public static PullRequestStatusOptions Default { get; } = new(TimeSpan.FromSeconds(30));
}
