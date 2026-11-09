namespace Kontext.UiTests.Tests;

public sealed class LaunchOnStartupReadBackTests : KontextFixture
{
    protected override void BeforeLaunch() => Data.RegisterStartup("\"C:\\somewhere\\Kontext.exe\" --startup");

    [Test]
    public void ShowsTheRealStateWindowsHasInsteadOfADefault()
    {
        App.Settings.Open();

        Assert.That(App.Settings.LaunchOnStartupIsOn, Is.True);
    }
}
