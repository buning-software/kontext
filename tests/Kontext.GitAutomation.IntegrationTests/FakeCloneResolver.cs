using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.IntegrationTests;

internal sealed class FakeCloneResolver(RepositoryRef answer) : IRepositoryCloneResolver
{
    public Task<RepositoryRef> ResolveAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
        => Task.FromResult(answer);
}
