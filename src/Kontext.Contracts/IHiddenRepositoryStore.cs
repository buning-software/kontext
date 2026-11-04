namespace Kontext.Contracts;

/// <summary>Remembers which repositories the user chose to hide from the issue menu, by <c>RepositoryRef.DisplayName</c>.</summary>
public interface IHiddenRepositoryStore
{
    IReadOnlySet<string> Load();

    void Save(IReadOnlySet<string> hidden);
}
