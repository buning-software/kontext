using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeProvisioner : IWorkspaceProvisioner
{
    public WorktreeInfo Result { get; set; } = new(@"C:\wt\default", "kontext/0-default", "abc", IsMain: false);

    public Exception? FailWith { get; set; }

    public Task<WorktreeInfo> ProvisionAsync(GitHubIssue issue, CancellationToken cancellationToken = default)
        => FailWith is null ? Task.FromResult(Result) : Task.FromException<WorktreeInfo>(FailWith);
}
