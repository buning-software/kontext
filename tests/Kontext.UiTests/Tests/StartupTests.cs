using static Kontext.UiTests.Application.FakeGitHub;

namespace Kontext.UiTests.Tests;

public sealed class StartupTests : KontextFixture
{
    [Test]
    public void LaunchOpensSignedInWithTheIssuesGroupedByRepository()
    {
        Assert.That(App.Sidebar.IssueCount, Is.EqualTo(AllIssuesCount));
        Assert.That(App.Sidebar.HasGroup(Gallery), Is.True);
        Assert.That(App.Sidebar.HasGroup(Memento), Is.True);
        Assert.That(App.Sidebar.HasIssue(AddNotifications), Is.True);
    }
}
