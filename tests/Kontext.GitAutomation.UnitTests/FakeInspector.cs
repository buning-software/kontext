using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeInspector : IGitRepositoryInspector
{
    private readonly Dictionary<string, (string Root, RepositoryRef[] Remotes)> _folders = new(StringComparer.OrdinalIgnoreCase);

    public FakeInspector WithRepository(string folder, string root, params RepositoryRef[] remotes)
    {
        _folders[folder] = (root, remotes);
        return this;
    }

    public Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken = default)
        => Task.FromResult(_folders.TryGetValue(folder, out var entry) ? entry.Root : null);

    public Task<IReadOnlyList<RepositoryRef>> GetGitHubRemotesAsync(string repositoryRoot, string host = GitHubHosts.Default, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RepositoryRef>>(
            (_folders.Values.FirstOrDefault(entry => entry.Root == repositoryRoot).Remotes ?? []).Where(remote => string.Equals(remote.Host, host, StringComparison.OrdinalIgnoreCase)).ToArray());
}
