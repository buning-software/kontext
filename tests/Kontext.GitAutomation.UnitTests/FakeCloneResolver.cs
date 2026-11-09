using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeCloneResolver(RepositoryRef answer) : IRepositoryCloneResolver
{
    public Task<RepositoryRef> ResolveAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
        => Task.FromResult(answer);
}
