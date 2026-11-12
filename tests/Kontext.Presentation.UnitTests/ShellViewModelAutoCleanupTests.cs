namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class ShellViewModelAutoCleanupTests : ShellViewModelFixture
{
    [Test]
    public void ShowsAutomaticCleanupAsOnByDefault()
    {
        Assert.That(Shell.CleanUpAutomatically, Is.True);
    }

    [Test]
    public void SavesTheChoiceWhenAutomaticCleanupIsTurnedOff()
    {
        var changed = new List<string?>();
        Shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Shell.CleanUpAutomatically = false;

        Assert.That(AutoCleanupSetting.IsEnabled, Is.False);
        Assert.That(changed, Does.Contain(nameof(ShellViewModel.CleanUpAutomatically)));
    }
}
