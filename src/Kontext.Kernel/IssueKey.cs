namespace Kontext.Kernel;

public readonly record struct IssueKey
{
    public IssueKey(string repositoryFullName, int number, string host = GitHubHosts.Default)
    {
        RepositoryFullName = repositoryFullName.ToUpperInvariant();
        Number = number;
        Host = host.ToUpperInvariant();
    }

    public IssueKey(RepositoryRef repository, int number)
        : this(repository.FullName, number, repository.Host)
    {
    }

    public string RepositoryFullName { get; }

    public int Number { get; }

    public string Host { get; }

    public override string ToString() => GitHubHosts.IsDefault(Host) ? $"{RepositoryFullName}#{Number}" : $"{Host}/{RepositoryFullName}#{Number}";
}
