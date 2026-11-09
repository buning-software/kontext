using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeWorktreeService : IGitWorktreeService
{
    private readonly Dictionary<string, IReadOnlyList<WorktreeInfo>> _listings = new(StringComparer.OrdinalIgnoreCase);

    public WorktreeRequest? LastRequest { get; private set; }

    public List<string> ListedPaths { get; } = [];

    public FakeWorktreeService WithListing(string repositoryPath, params WorktreeInfo[] worktrees)
    {
        _listings[repositoryPath] = worktrees;
        return this;
    }

    public Task<WorktreeInfo> CreateAsync(WorktreeRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new WorktreeInfo(@"C:\wt", request.BranchName, "abc", IsMain: false));
    }

    public Task<IReadOnlyList<WorktreeInfo>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ListedPaths.Add(repositoryPath);

        return _listings.TryGetValue(repositoryPath, out var listing)
            ? Task.FromResult(listing)
            : Task.FromException<IReadOnlyList<WorktreeInfo>>(new GitAutomationException($"cannot read {repositoryPath}"));
    }

    public List<(string Path, bool Force)> Removed { get; } = [];

    public List<string> Pruned { get; } = [];

    public Exception? RemoveFailsWith { get; set; }

    public Task RemoveAsync(string worktreePath, bool force = false, CancellationToken cancellationToken = default)
    {
        Removed.Add((worktreePath, force));
        return RemoveFailsWith is null ? Task.CompletedTask : Task.FromException(RemoveFailsWith);
    }

    public Task PruneAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        Pruned.Add(repositoryPath);
        return Task.CompletedTask;
    }
}
