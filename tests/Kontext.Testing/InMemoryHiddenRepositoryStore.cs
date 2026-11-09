using System.Collections.Concurrent;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class InMemoryHiddenRepositoryStore(params string[] hidden) : IHiddenRepositoryStore
{
    public HashSet<string> Hidden { get; private set; } = new(hidden, StringComparer.OrdinalIgnoreCase);

    public int Saves { get; private set; }

    public IReadOnlySet<string> Load() => new HashSet<string>(Hidden, StringComparer.OrdinalIgnoreCase);

    public void Save(IReadOnlySet<string> hidden)
    {
        Hidden = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
        Saves++;
    }
}
