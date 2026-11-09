using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeExistingWorkspaceFinder : IExistingWorkspaceFinder
{
    public List<ExistingWorkspace> Found { get; } = [];

    public Task<IReadOnlyList<ExistingWorkspace>> FindAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ExistingWorkspace>>(Found.ToList());
}
