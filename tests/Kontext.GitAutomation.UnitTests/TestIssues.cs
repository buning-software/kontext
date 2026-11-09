using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal static class TestIssues
{
    public static GitHubIssue Create(int number = 7, string title = "Fix login crash", string owner = "acme", string name = "app")
        => new(new RepositoryRef(owner, name), number, title, Body: null, State: "open", Labels: [], new Uri($"https://github.com/{owner}/{name}/issues/{number}"), Author: "someone", UpdatedAt: DateTimeOffset.UnixEpoch);
}
