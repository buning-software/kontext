using static Kontext.UiTests.Application.FakeGitHub;

namespace Kontext.UiTests.Tests;

public sealed class IssueDetailTests : KontextFixture
{
    [Test]
    public void ClickingAnIssueShowsItInTheDetailPane()
    {
        Assert.That(App.IssueDetail.ShowsNoSelection, Is.True);

        App.Sidebar.Issue(AddNotifications).Click();

        Eventually(() => App.IssueDetail.Title == AddNotifications);
    }
}
