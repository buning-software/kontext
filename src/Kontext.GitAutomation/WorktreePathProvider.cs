using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class WorktreePathProvider(GitAutomationOptions options) : IWorktreePathProvider
{
    public string GetPath(RepositoryRef repository, string branchName)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        var folder = branchName[(branchName.LastIndexOf('/') + 1)..];

        var root = GitHubHosts.IsDefault(repository.Host)
            ? options.WorktreesRoot
            : Path.Combine(options.WorktreesRoot, PathSegment.Safe(repository.Host));

        return Path.Combine(root, PathSegment.Safe(repository.Owner), PathSegment.Safe(repository.Name), PathSegment.Safe(folder));
    }
}
