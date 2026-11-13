using Kontext.Domain;
using Kontext.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class PullRequestStatusViewModelTests
{
    private static readonly Uri PullUrl = new("https://github.com/acme/gallery/pull/12");

    private FakeGitHubService _github = null!;
    private PullRequestStatusViewModel _status = null!;

    [SetUp]
    public void CreateStatus()
    {
        _github = new FakeGitHubService();
        _status = new PullRequestStatusViewModel(_github, new FakeTimeProvider(), PullRequestStatusOptions.Default);
    }

    [TearDown]
    public void DisposeStatus() => _status.Dispose();

    [Test]
    public void IsHiddenWithoutABranch()
    {
        _status.Track(Issues.Create(), null);

        Assert.That(_status.IsVisible, Is.False);
        Assert.That(_github.PullRequestCalls, Is.Zero);
    }

    [Test]
    public async Task ShowsThePullRequestAndItsWorkflowResult()
    {
        _github.PullRequest = new PullRequestStatus(12, PullUrl, PullRequestState.Open, ChecksState.Failing);

        _status.Track(Issues.Create(), "features/4-add");

        await AsyncAssert.EventuallyAsync(() => _status.HasPullRequest, "the pull request loads");
        Assert.That(_status.Text, Is.EqualTo("Pull request #12 · open · workflow failed"));
        Assert.That(_status.Url, Is.EqualTo(PullUrl));
    }

    [Test]
    public async Task SaysThereIsNoPullRequestYetWhenTheBranchHasNone()
    {
        _status.Track(Issues.Create(), "features/4-add");

        await AsyncAssert.EventuallyAsync(() => _github.PullRequestCalls == 1, "the lookup ran");
        Assert.That(_status.Text, Is.EqualTo("No pull request yet"));
        Assert.That(_status.HasNoPullRequest, Is.True);
    }

    [Test]
    public async Task TrackingTheSameBranchAgainDoesNotLookItUpAgain()
    {
        var issue = Issues.Create();
        _status.Track(issue, "features/4-add");
        await AsyncAssert.EventuallyAsync(() => _github.PullRequestCalls == 1, "the lookup ran");

        _status.Track(issue, "features/4-add");

        Assert.That(_github.PullRequestCalls, Is.EqualTo(1));
    }
}
