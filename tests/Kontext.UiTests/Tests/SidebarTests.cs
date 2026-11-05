using System.Diagnostics;
using static Kontext.UiTests.Application.FakeGitHub;

namespace Kontext.UiTests.Tests;

public sealed class SidebarTests : KontextFixture
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    [Test]
    public void RefreshKeepsEachRepositoryExpandedOrCollapsed()
    {
        string[] collapsed = [Beacon, Gallery];
        foreach (var repository in collapsed)
        {
            App.Sidebar.Group(repository).Collapse();
        }

        Eventually(() => collapsed.All(repository => !App.Sidebar.IsExpanded(repository)));

        for (var refresh = 1; refresh <= 6; refresh++)
        {
            App.Sidebar.Refresh();

            // A refresh with unchanged data leaves nothing to wait for, so watch the state for a while instead.
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < Settle)
            {
                foreach (var repository in Repositories)
                {
                    Assert.That(
                        App.Sidebar.IsExpanded(repository),
                        Is.EqualTo(!collapsed.Contains(repository)),
                        $"{repository} expanded after refresh {refresh}");
                }
            }
        }
    }

    [Test]
    public void SearchNarrowsTheListAndClearingItShowsEverythingAgain()
    {
        App.Sidebar.Search("galleries");

        // Wait for the whole end state: rows are briefly missing while the menu lays out the narrowed list.
        Eventually(() => !App.Sidebar.HasIssue(AddNotifications) && !App.Sidebar.HasGroup(Memento) && App.Sidebar.HasIssue(CreatingGalleries));

        App.Sidebar.Search(string.Empty);

        Eventually(() => App.Sidebar.HasIssue(AddNotifications) && App.Sidebar.HasIssue(BringUnderUiTest));
        Assert.That(App.Sidebar.IssueCount, Is.EqualTo(AllIssuesCount));
    }
}
