using Kontext.Contracts;

namespace Kontext.Sessions;

internal sealed class JsonHiddenRepositoryStore(SessionsOptions options) : IHiddenRepositoryStore
{
    private readonly JsonNameSetFile _file = new(options.StateDirectory, "hidden-repositories.json");

    public IReadOnlySet<string> Load() => _file.Load();

    public void Save(IReadOnlySet<string> hidden) => _file.Save(hidden);
}
