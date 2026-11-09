using System.Collections.Concurrent;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class InMemoryCollapsedRepositoryStore(params string[] collapsed) : ICollapsedRepositoryStore
{
    public HashSet<string> Collapsed { get; private set; } = new(collapsed, StringComparer.OrdinalIgnoreCase);

    public int Saves { get; private set; }

    public IReadOnlySet<string> Load() => new HashSet<string>(Collapsed, StringComparer.OrdinalIgnoreCase);

    public void Save(IReadOnlySet<string> collapsed)
    {
        Collapsed = new HashSet<string>(collapsed, StringComparer.OrdinalIgnoreCase);
        Saves++;
    }
}
