using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeBranchNamer(string branch, int? issueNumber = null) : IBranchNamer
{
    public string Name(GitHubIssue issue) => branch;

    public int? IssueNumberOf(string branchName) => issueNumber;
}
