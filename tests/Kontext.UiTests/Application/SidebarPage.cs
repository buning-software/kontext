using WinUia;
using WinUia.Patterns;

namespace Kontext.UiTests.Application;

/// <summary>The issue sidebar: repository groups, issues, search, filter and refresh.</summary>
public sealed class SidebarPage(Element window)
{
    public Element Root => window.FindByAutomationId("Sidebar");

    public string IssueCount => Root.FindByAutomationId("IssueCount").Name;

    public Element Group(string repository) => Root.Find(e => e.ControlType == ControlType.ListItem && e.Name == repository);

    public bool HasGroup(string repository) =>
        Root.TryFind(e => e.ControlType == ControlType.ListItem && e.Name == repository, timeout: TimeSpan.Zero) is not null;

    public bool IsExpanded(string repository) => Group(repository).ExpandCollapsePattern.State == ExpandCollapseState.Expanded;

    public Element Issue(string title) => Root.Find(e => e.ControlType == ControlType.ListItem && e.Name == title);

    public bool HasIssue(string title) =>
        Root.TryFind(e => e.ControlType == ControlType.ListItem && e.Name == title, timeout: TimeSpan.Zero) is not null;

    public void Refresh() => Root.FindByAutomationId("RefreshButton").Click();

    public void Search(string text) =>
        Root.FindByAutomationId("SearchBox").Find(e => e.ControlType == ControlType.Edit).SetValue(text);

    public RepositoryFilterPage OpenFilter()
    {
        Root.FindByAutomationId("FilterButton").Click();
        return new RepositoryFilterPage(window);
    }
}
