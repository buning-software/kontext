using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.UiTesting;

/// <summary>Never clones, starts Claude or opens a terminal: UI tests must not touch the machine.</summary>
internal sealed class UiTestSessionCoordinator : IIssueSessionCoordinator
{
    public Task StartOrResumeAsync(GitHubIssue issue, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
