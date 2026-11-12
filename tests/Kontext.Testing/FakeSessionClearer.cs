using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeSessionClearer : IIssueSessionClearer
{
    public List<GitHubIssue> Cleared { get; } = [];

    /// <summary>When set, a clear waits for the gate after recording the call.</summary>
    public TaskCompletionSource<bool>? Gate { get; set; }

    /// <summary>When set, every clear throws this after recording the call.</summary>
    public Exception? FailWith { get; set; }

    public async Task ClearAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
    {
        Cleared.Add(issue);

        if (Gate is { } gate)
        {
            await gate.Task;
        }

        if (FailWith is { } failure)
        {
            throw failure;
        }
    }
}
