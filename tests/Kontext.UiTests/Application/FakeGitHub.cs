namespace Kontext.UiTests.Application;

/// <summary>What Kontext's fake GitHub (src/Kontext/UiTesting/UiTestGitHubData.cs) serves in UI test mode.</summary>
public static class FakeGitHub
{
    public const string Atlas = "acme/atlas";
    public const string Beacon = "acme/beacon";
    public const string Compass = "acme/compass";
    public const string Gallery = "acme/gallery";
    public const string Memento = "acme/memento";

    public const string CreatingGalleries = "Creating Galleries";
    public const string AddNotifications = "Add notifications";
    public const string BringUnderUiTest = "Bring project under UI test";

    public const string AllIssuesCount = "9 open issues";

    /// <summary>The account card title of the signed-in github.com user.</summary>
    public const string Account = "ui-tester - github.com";

    public static IReadOnlyList<string> Repositories { get; } = [Atlas, Beacon, Compass, Gallery, Memento];
}
