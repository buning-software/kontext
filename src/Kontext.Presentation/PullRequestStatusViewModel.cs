using CommunityToolkit.Mvvm.ComponentModel;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

/// <summary>
/// The pull request of an issue's branch and the result of its workflow runs, polled while both an issue and a branch are set.
/// Await continuations return to the thread that called <see cref="Track"/> (the UI thread), so no dispatcher is needed.
/// </summary>
public sealed partial class PullRequestStatusViewModel(IGitHubService github, TimeProvider clock, PullRequestStatusOptions options) : ObservableObject, IDisposable
{
    private CancellationTokenSource? _loop;
    private GitHubIssue? _issue;
    private string? _branch;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible), nameof(HasPullRequest), nameof(HasNoPullRequest), nameof(Text), nameof(Checks))]
    public partial PullRequestStatus? Status { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text))]
    public partial bool IsUnavailable { get; set; }

    public bool IsVisible => _branch is not null;

    public bool HasPullRequest => Status is not null;

    public bool HasNoPullRequest => IsVisible && Status is null;

    public Uri? Url => Status?.Url;

    public ChecksState Checks => Status?.Checks ?? ChecksState.None;

    public string Text => Status is { } status
        ? $"Pull request #{status.Number} · {StateText(status.State)} · {ChecksText(status.Checks)}"
        : IsUnavailable ? "Pull request status unavailable" : "No pull request yet";

    public void Track(GitHubIssue? issue, string? branchName)
    {
        var branch = string.IsNullOrEmpty(branchName) ? null : branchName;
        if (_disposed || (_issue?.Key == issue?.Key && _branch == branch))
        {
            return;
        }

        StopLoop();
        _issue = branch is null ? null : issue;
        _branch = _issue is null ? null : branch;
        Status = null;
        IsUnavailable = false;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(HasNoPullRequest));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Url));

        if (_issue is not null && _branch is not null)
        {
            var loop = _loop = new CancellationTokenSource();
            _ = RunLoopAsync(_issue.Repository, _branch, loop.Token);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        StopLoop();
    }

    partial void OnStatusChanged(PullRequestStatus? value) => OnPropertyChanged(nameof(Url));

    private void StopLoop()
    {
        if (_loop is { } loop)
        {
            _loop = null;
            loop.Cancel();
            loop.Dispose();
        }
    }

    private async Task RunLoopAsync(RepositoryRef repository, string branch, CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(options.PollInterval, clock);

            await ReloadAsync(repository, branch, token);
            while (await timer.WaitForNextTickAsync(token))
            {
                await ReloadAsync(repository, branch, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReloadAsync(RepositoryRef repository, string branch, CancellationToken token)
    {
        try
        {
            var status = await github.GetPullRequestStatusAsync(repository, branch, token);
            if (!token.IsCancellationRequested)
            {
                Status = status;
                IsUnavailable = false;
            }
        }
        catch (KontextException)
        {
            // A poll that fails keeps the last known status; only the first one has nothing to keep.
            if (!token.IsCancellationRequested && Status is null)
            {
                IsUnavailable = true;
            }
        }
    }

    private static string StateText(PullRequestState state) => state switch
    {
        PullRequestState.Draft => "draft",
        PullRequestState.Merged => "merged",
        PullRequestState.Closed => "closed",
        _ => "open",
    };

    private static string ChecksText(ChecksState checks) => checks switch
    {
        ChecksState.Pending => "workflow running",
        ChecksState.Passing => "workflow passed",
        ChecksState.Failing => "workflow failed",
        _ => "no workflow",
    };
}
