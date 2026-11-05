using System.Globalization;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.UiTesting;

/// <summary>Fixed GitHub data the app shows when it runs under UI test (<c>KONTEXT_UI_TEST=1</c>).</summary>
internal static class UiTestGitHubData
{
    public static GitHubUser User { get; } = new("ui-tester", "UI Tester", new Uri("https://github.com/ui-tester"));

    public static IReadOnlyList<GitHubIssue> Issues { get; } =
    [
        Create("acme", "atlas", 12, "Draw the coastline", "2025-02-24T10:00:00Z"),
        Create("acme", "atlas", 11, "Add a legend", "2025-02-14T10:00:00Z"),
        Create("acme", "beacon", 7, "Blink on new mail", "2025-02-23T10:00:00Z"),
        Create("acme", "beacon", 6, "Dim at night", "2025-02-13T10:00:00Z"),
        Create("acme", "compass", 21, "Point north", "2025-02-22T10:00:00Z"),
        Create("acme", "compass", 20, "Calibrate on start", "2025-02-12T10:00:00Z"),
        Create("acme", "gallery", 159, "Creating Galleries", "2025-02-25T10:00:00Z", "enhancement"),
        Create("acme", "memento", 4, "Add notifications", "2025-02-20T10:00:00Z"),
        Create("acme", "memento", 3, "Bring project under UI test", "2025-02-10T10:00:00Z"),
    ];

    private static GitHubIssue Create(string owner, string name, int number, string title, string updatedAt, params string[] labels) =>
        new(
            new RepositoryRef(owner, name),
            number,
            title,
            $"Body of {title}.",
            "open",
            labels,
            new Uri($"https://github.com/{owner}/{name}/issues/{number}"),
            "octocat",
            DateTimeOffset.Parse(updatedAt, CultureInfo.InvariantCulture));
}
