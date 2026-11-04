namespace Kontext.Contracts;

/// <summary>Remembers which repository groups the user collapsed in the issue menu, by <c>RepositoryRef.DisplayName</c>.</summary>
public interface ICollapsedRepositoryStore
{
    IReadOnlySet<string> Load();

    void Save(IReadOnlySet<string> collapsed);
}
