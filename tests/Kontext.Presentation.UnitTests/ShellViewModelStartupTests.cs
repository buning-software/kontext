using Kontext.Domain;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class ShellViewModelStartupTests : ShellViewModelFixture
{
    [Test]
    public void IsOffByDefault() => Assert.That(Shell.LaunchOnStartup, Is.False);

    [Test]
    public async Task TurnsLaunchOnStartupOnAndOff()
    {
        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(true);

        Assert.That(Shell.LaunchOnStartup, Is.True);
        Assert.That(Startup.State, Is.EqualTo(StartupState.Enabled));
        Assert.That(Shell.HasError, Is.False);

        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(false);

        Assert.That(Shell.LaunchOnStartup, Is.False);
        Assert.That(Startup.State, Is.EqualTo(StartupState.Disabled));
    }

    [Test]
    public async Task ReadsTheRealStateBackFromWindows()
    {
        Startup.State = StartupState.Enabled;
        await Shell.RefreshStartupCommand.ExecuteAsync(null);
        Assert.That(Shell.LaunchOnStartup, Is.True);

        Startup.State = StartupState.DisabledByUser;
        await Shell.RefreshStartupCommand.ExecuteAsync(null);
        Assert.That(Shell.LaunchOnStartup, Is.False);
        Assert.That(Shell.HasError, Is.False);
    }

    [TestCase(StartupState.DisabledByUser, "Startup")]
    [TestCase(StartupState.DisabledByPolicy, "policy")]
    public async Task StaysOffAndExplainsWhyWhenWindowsBlocksIt(StartupState blocked, string reason)
    {
        Startup.ResultOfEnabling = blocked;

        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(true);

        Assert.That(Shell.LaunchOnStartup, Is.False);
        Assert.That(Shell.HasError, Is.True);
        Assert.That(Shell.ErrorMessage, Does.Contain(reason));
    }

    [Test]
    public async Task StaysOffAndReportsAFailure()
    {
        Startup.FailsWith = new InvalidOperationException("registry locked");

        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(true);

        Assert.That(Shell.LaunchOnStartup, Is.False);
        Assert.That(Shell.ErrorMessage, Does.Contain("registry locked"));
    }

    [Test]
    public async Task ClearsAnEarlierErrorOnTheNextSuccessfulChange()
    {
        Startup.ResultOfEnabling = StartupState.DisabledByPolicy;
        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(true);
        Startup.ResultOfEnabling = null;

        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(true);

        Assert.That(Shell.LaunchOnStartup, Is.True);
        Assert.That(Shell.HasError, Is.False);
    }

    [Test]
    public async Task ExplainsWhyWhenWindowsKeepsItEnabledAfterTurningItOff()
    {
        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(true);
        Startup.ResultOfDisabling = StartupState.Enabled;

        await Shell.SetLaunchOnStartupCommand.ExecuteAsync(false);

        Assert.That(Shell.LaunchOnStartup, Is.True);
        Assert.That(Shell.ErrorMessage, Does.Contain("kept launch on startup enabled"));
    }
}
