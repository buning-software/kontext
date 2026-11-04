using CommunityToolkit.Mvvm.ComponentModel;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class IssueItemViewModel : ObservableObject
{
    public IssueItemViewModel(GitHubIssue issue, IssueWorkState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(state);

        Issue = issue;
        Key = issue.Key;
        State = state;
        Subtitle = BuildSubtitle(issue, now);
    }

    public GitHubIssue Issue { get; }

    public IssueKey Key { get; }

    public string Title => Issue.Title;

    public string NumberText => $"#{Issue.Number}";

    public string Subtitle { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(StatusText))]
    public partial IssueWorkState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Status), nameof(StatusText))]
    public partial bool HasSavedSession { get; set; }

    /// <summary>
    /// What the menu shows. A ready environment whose Claude session was saved earlier shows as waiting for the user,
    /// which is what resuming it gives; the tracked state itself stays Ready so that selecting the issue still resumes it.
    /// </summary>
    public IssueWorkStatus Status => State.Status switch
    {
        IssueWorkStatus.Ready or IssueWorkStatus.NeedsInput when Issue.IsClosed => IssueWorkStatus.Done,
        IssueWorkStatus.Ready when HasSavedSession && !State.SessionEnded => IssueWorkStatus.NeedsInput,
        var other => other,
    };

    public string StatusText => IssueStatusText.For(Status);

    private static string BuildSubtitle(GitHubIssue issue, DateTimeOffset now)
    {
        var subtitle = $"updated {RelativeTime.Format(issue.UpdatedAt, now)}";
        return issue.Author is null ? subtitle : $"{subtitle} · by {issue.Author}";
    }
}
