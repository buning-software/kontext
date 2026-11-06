using Kontext.UiTests.Application;
using static Kontext.UiTests.Application.FakeGitHub;

namespace Kontext.UiTests.Tests;

/// <summary>
/// The file list is virtualised and the CI window is small, so each test only relies on the first file row (Map),
/// plus Legend once Map has been committed and Legend has moved up to take its place.
/// </summary>
public sealed class GitTabTests : KontextFixture
{
    private const string Map = "src/Map.cs";
    private const string Legend = "src/Legend.cs";

    [SetUp]
    public void OpenGitTab()
    {
        App.Sidebar.Issue(DrawTheCoastline).Click();
        Eventually(() => App.IssueDetail.Title == DrawTheCoastline, "the issue opens");
        App.GitTab.Open();
        Eventually(() => App.GitTab.HasFile(Map), "the changed files are listed");
    }

    [Test]
    public void ListsTheChangedFilesAndDisablesCommitAndPushUntilApplicable()
    {
        Assert.That(App.GitTab.CommitButton.IsEnabled, Is.False, "nothing selected, no message");
        Assert.That(App.GitTab.PushButton.IsEnabled, Is.False, "nothing to push");

        App.GitTab.Check(Map);
        Assert.That(App.GitTab.CommitButton.IsEnabled, Is.False, "no message yet");

        App.GitTab.TypeMessage("Draw the map");
        Eventually(() => App.GitTab.CommitButton.IsEnabled, "commit is enabled with a selection and a message");
    }

    [Test]
    public void CommitsTheSelectedFilesAndThenPushesThem()
    {
        App.GitTab.Check(Map);
        App.GitTab.TypeMessage("Draw the map");
        Eventually(() => App.GitTab.CommitButton.IsEnabled, "commit is enabled");

        App.GitTab.CommitButton.Click();

        Eventually(() => !App.GitTab.HasFile(Map), "the committed file leaves the list");
        Eventually(() => App.GitTab.HasFile(Legend), "unselected files stay");
        Eventually(() => App.GitTab.Status == "Committed 1 file.", "the commit is confirmed");
        Eventually(() => App.GitTab.PushButton.IsEnabled, "push is enabled after a commit");

        App.GitTab.PushButton.Click();

        Eventually(() => App.GitTab.Status == "Pushed to origin.", "the push is confirmed");
        Eventually(() => !App.GitTab.PushButton.IsEnabled, "nothing is left to push");
    }

    [Test]
    public void ShowsAFailedCommitAndKeepsTheSelection()
    {
        App.GitTab.Check(Map);
        App.GitTab.TypeMessage("This will fail");
        Eventually(() => App.GitTab.CommitButton.IsEnabled, "commit is enabled");

        App.GitTab.CommitButton.Click();

        Eventually(() => App.GitTab.Error is { Length: > 0 }, "the error is shown");
        Assert.That(App.GitTab.Error, Does.Contain("Commit failed"));
        Assert.That(App.GitTab.HasFile(Map), Is.True);
        Assert.That(App.GitTab.IsChecked(Map), Is.True);
        Assert.That(App.GitTab.CommitButton.IsEnabled, Is.True, "the user can retry");
    }

    [Test]
    public void ShowsARejectedPushAndStaysPushable()
    {
        App.GitTab.Check(Map);
        App.GitTab.TypeMessage("Draw the map (reject)");
        Eventually(() => App.GitTab.CommitButton.IsEnabled, "commit is enabled");
        App.GitTab.CommitButton.Click();
        Eventually(() => App.GitTab.PushButton.IsEnabled, "push is enabled after a commit");

        App.GitTab.PushButton.Click();

        Eventually(() => App.GitTab.Error is { Length: > 0 }, "the error is shown");
        Assert.That(App.GitTab.Error, Does.Contain("Push rejected"));
        Assert.That(App.GitTab.PushButton.IsEnabled, Is.True, "the commit is still unpushed");
    }
}
