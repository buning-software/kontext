namespace Kontext.Kernel;

public sealed record RepositoryRef(string Owner, string Name, string? LocalPath = null, string Host = GitHubHosts.Default)
{
    public string FullName => $"{Owner}/{Name}";

    public string DisplayName => GitHubHosts.IsDefault(Host) ? FullName : $"{Host}/{FullName}";
}
