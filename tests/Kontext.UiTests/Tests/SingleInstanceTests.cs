using Kontext.UiTests.Application;
using WinUia;

namespace Kontext.UiTests.Tests;

public sealed class SingleInstanceTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private TestDataDirectory _data = null!;

    [SetUp]
    public void CreateData() => _data = new TestDataDirectory();

    [TearDown]
    public void DeleteData() => _data.Dispose();

    [Test]
    public void ALaterLaunchExitsAndLeavesTheFirstInstanceRunning()
    {
        using var first = KontextProcess.Start(_data);
        Assert.That(Poll.Until(() => first.HasWindow, Patience), Is.True, "the first instance shows its window");

        using var second = KontextProcess.Start(_data);

        Assert.That(second.WaitForExit(Patience), Is.True, "the second launch exits by itself");
        Assert.That(second.ExitCode, Is.Zero);
        Assert.That(first.HasExited, Is.False);
    }

    [Test]
    public void AfterTheFirstInstanceHasExitedAnotherLaunchStartsNormally()
    {
        using (var first = KontextProcess.Start(_data))
        {
            Assert.That(Poll.Until(() => first.HasWindow, Patience), Is.True);
        }

        using var again = KontextProcess.Start(_data);

        Assert.That(Poll.Until(() => again.HasWindow, Patience), Is.True, "a fresh instance shows its window");
        Assert.That(again.HasExited, Is.False);
    }

    [Test]
    public void StartingAtSignInStaysHiddenAndALaterLaunchBringsTheWindowUp()
    {
        using var atSignIn = KontextProcess.Start(_data, "--startup");
        Thread.Sleep(TimeSpan.FromSeconds(8));

        Assert.That(atSignIn.HasExited, Is.False);
        Assert.That(atSignIn.HasWindow, Is.False, "a sign-in launch stays in the tray");

        using var manual = KontextProcess.Start(_data);

        Assert.That(manual.WaitForExit(Patience), Is.True);
        Assert.That(Poll.Until(() => atSignIn.HasWindow, Patience), Is.True, "the manual launch restores the hidden window");
    }

    [Test]
    public void ASignInLaunchRedirectedToARunningInstanceDoesNotPopItsWindowUp()
    {
        using var first = KontextProcess.Start(_data, "--startup");
        Thread.Sleep(TimeSpan.FromSeconds(8));

        using var second = KontextProcess.Start(_data, "--startup");

        Assert.That(second.WaitForExit(Patience), Is.True);
        Thread.Sleep(TimeSpan.FromSeconds(3));
        Assert.That(first.HasWindow, Is.False);
    }
}
