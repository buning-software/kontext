using Kontext.Contracts;

namespace Kontext.Testing;

public sealed class FakeWorkspaceBranches : IWorkspaceBranches
{
    public string Branch { get; set; } = "kontext/7-some-work";

    /// <summary>What <see cref="FindUnpublishedBaseAsync"/> answers: null once the branch is on GitHub.</summary>
    public string? UnpublishedBase { get; set; } = "base123";

    public Task<string> GetBranchAsync(string worktreePath, CancellationToken cancellationToken = default) => Task.FromResult(Branch);

    public Task<string?> FindUnpublishedBaseAsync(string worktreePath, string branchName, CancellationToken cancellationToken = default) => Task.FromResult(UnpublishedBase);
}
