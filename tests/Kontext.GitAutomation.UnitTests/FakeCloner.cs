using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitAutomation.UnitTests;

internal sealed class FakeCloner(string clonedTo) : IRepositoryCloner
{
    public int Calls { get; private set; }

    public RepositoryRef? Last { get; private set; }

    public Exception? FailWith { get; set; }

    public Task<string> CloneAsync(RepositoryRef repository, CancellationToken cancellationToken = default)
    {
        Calls++;
        Last = repository;
        return FailWith is null ? Task.FromResult(clonedTo) : Task.FromException<string>(FailWith);
    }
}
