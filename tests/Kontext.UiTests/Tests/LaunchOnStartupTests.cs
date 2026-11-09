namespace Kontext.UiTests.Tests;

public sealed class LaunchOnStartupTests : KontextFixture
{
    [Test]
    public void IsOffOnAFreshInstall()
    {
        App.Settings.Open();

        Assert.That(App.Settings.LaunchOnStartupIsOn, Is.False);
        Assert.That(Data.RegisteredStartupCommand, Is.Null);
    }

    [Test]
    public void SwitchingItOnRegistersAutostartAndSwitchingItOffRemovesIt()
    {
        App.Settings.Open();

        App.Settings.SetLaunchOnStartup(true);

        Eventually(() => Data.RegisteredStartupCommand is not null, "the Run key value is written");
        Assert.That(Data.RegisteredStartupCommand, Does.EndWith("--startup"));
        Assert.That(Data.RegisteredStartupCommand, Does.StartWith("\"").And.Contain("Kontext.exe"));

        App.Settings.SetLaunchOnStartup(false);

        Eventually(() => Data.RegisteredStartupCommand is null, "the Run key value is removed");
    }
}
