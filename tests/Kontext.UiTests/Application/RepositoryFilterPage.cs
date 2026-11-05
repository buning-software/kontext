using WinUia;
using WinUia.Input;
using WinUia.Patterns;

namespace Kontext.UiTests.Application;

/// <summary>The repository filter flyout, opened from the sidebar: a card per account with a checkbox per repository.</summary>
public sealed class RepositoryFilterPage(Element window)
{
    public Element Repository(string repository) => CheckBox(repository);

    /// <summary>The header checkbox of an account card, found by its title (<c>login - host</c>).</summary>
    public Element Account(string title) => CheckBox(title);

    public void Hide(string repository) => SetChecked(Repository(repository), false);

    public void HideAccount(string title) => SetChecked(Account(title), false);

    public void ShowAccount(string title) => SetChecked(Account(title), true);

    public void ClearFilters() => window.FindByAutomationId("ClearFiltersButton").Click();

    public void Close() => window.Context.Input.SendKeys(VirtualKey.Escape);

    private Element CheckBox(string name) => window.Find(e => e.ControlType == ControlType.CheckBox && e.Name == name);

    private static void SetChecked(Element checkBox, bool isChecked)
    {
        if ((checkBox.TogglePattern.State == ToggleState.On) != isChecked)
        {
            checkBox.Toggle();
        }
    }
}
