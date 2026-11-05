using Kontext.UiTests.Application;

namespace Kontext.UiTests.Tests;

/// <summary>Launches a fresh Kontext on an empty data folder for every test, waits for its issues, and closes it afterwards.</summary>
public abstract class KontextFixture
{
    protected TestDataDirectory Data { get; private set; } = null!;

    protected KontextApp App { get; private set; } = null!;

    [SetUp]
    public void LaunchApp()
    {
        Data = new TestDataDirectory();
        App = KontextApp.Launch(Data);

        Eventually(() => App.Sidebar.IssueCount == FakeGitHub.AllIssuesCount, "the fake issues load on start-up", TimeSpan.FromSeconds(20));
    }

    [TearDown]
    public void CloseApp()
    {
        App.Dispose();
        Data.Dispose();
    }
}
