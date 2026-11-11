using WinUia;
using WinUia.Patterns;

namespace Kontext.UiTests.Application;

/// <summary>The git tab of the issue pane: changed files with checkboxes, the commit message, and Commit and Push.</summary>
public sealed class GitTabPage(Element window)
{
    public void Open() => window.FindByAutomationId("GitTab").Click();

    public Element File(string path) => window.Find(e => e.ControlType == ControlType.CheckBox && e.Name == path);

    public bool HasFile(string path) =>
        window.TryFind(e => e.ControlType == ControlType.CheckBox && e.Name == path, timeout: TimeSpan.Zero) is not null;

    public bool IsChecked(string path) => File(path).TogglePattern.State == ToggleState.On;

    public void Check(string path)
    {
        if (!IsChecked(path))
        {
            File(path).Toggle();
        }
    }

    public void TypeMessage(string message) => window.FindByAutomationId("GitCommitMessage").SetValue(message);

    public Element CommitButton => window.FindByAutomationId("GitCommitButton");

    public Element PushButton => window.FindByAutomationId("GitPushButton");

    /// <summary>The message in the error bar across the bottom of the window.</summary>
    public string? Error => window.TryFindByAutomationId("ErrorBar", TimeSpan.Zero)?.Name;

    public void CloseError() => window.Find(e => e.ControlType == ControlType.Button && e.Name == "Close").Click();

    public string? Status => window.TryFindByAutomationId("GitStatusMessage", TimeSpan.Zero)?.Name;
}
