using WinUia;

namespace Kontext.UiTests.Application;

/// <summary>The pane right of the sidebar that shows the selected issue.</summary>
public sealed class IssueDetailPage(Element window)
{
    public bool ShowsNoSelection => window.TryFindByAutomationId("NoIssueSelected", TimeSpan.Zero) is not null;

    public string? Title => window.TryFindByAutomationId("IssueTitle", TimeSpan.Zero)?.Name;
}
