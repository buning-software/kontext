using Kontext.Contracts;

namespace Kontext.Sessions;

internal sealed class JsonCollapsedRepositoryStore(SessionsOptions options) : ICollapsedRepositoryStore
{
    private readonly JsonNameSetFile _file = new(options.StateDirectory, "collapsed-repositories.json");

    public IReadOnlySet<string> Load() => _file.Load();

    public void Save(IReadOnlySet<string> collapsed) => _file.Save(collapsed);
}
