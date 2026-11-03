namespace Kontext.Contracts;

public interface IGitHubTokenStore
{
    string? Load(string host);

    void Save(string host, string token);

    void Clear(string host);

    string? FindEnterpriseHost();
}
