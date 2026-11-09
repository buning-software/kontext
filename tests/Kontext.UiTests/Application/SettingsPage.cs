using WinUia;
using WinUia.Patterns;

namespace Kontext.UiTests.Application;

/// <summary>The settings page, opened from the gear at the bottom of the sidebar.</summary>
public sealed class SettingsPage(Element window)
{
    public Element LaunchOnStartup => window.Find(e => e.ControlType == ControlType.Button && e.Name == "Launch on startup");

    public bool LaunchOnStartupIsOn => LaunchOnStartup.TogglePattern.State == ToggleState.On;

    public void Open() => window.FindByAutomationId("SettingsItem").Click();

    public void SetLaunchOnStartup(bool on)
    {
        if (LaunchOnStartupIsOn != on)
        {
            LaunchOnStartup.Toggle();
        }
    }
}
