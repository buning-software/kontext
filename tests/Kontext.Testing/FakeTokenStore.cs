using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeTokenStore(string? saved = null, string? savedEnterprise = null, string enterpriseHost = "github.corp.com") : IGitHubTokenStore
{
    public string? Saved { get; private set; } = saved;

    public string? SavedEnterprise { get; private set; } = savedEnterprise;

    public string? EnterpriseHost { get; private set; } = savedEnterprise is null ? null : enterpriseHost;

    public int Clears { get; private set; }

    public int EnterpriseClears { get; private set; }

    public Exception? LoadFailsWith { get; set; }

    public string? Load(string host)
    {
        if (LoadFailsWith is not null)
        {
            throw LoadFailsWith;
        }

        return GitHubHosts.IsDefault(host) ? Saved : string.Equals(host, EnterpriseHost, StringComparison.OrdinalIgnoreCase) ? SavedEnterprise : null;
    }

    public void Save(string host, string token)
    {
        if (GitHubHosts.IsDefault(host))
        {
            Saved = token;
            return;
        }

        EnterpriseHost = host;
        SavedEnterprise = token;
    }

    public void Clear(string host)
    {
        if (GitHubHosts.IsDefault(host))
        {
            Saved = null;
            Clears++;
            return;
        }

        SavedEnterprise = null;
        EnterpriseHost = null;
        EnterpriseClears++;
    }

    public string? FindEnterpriseHost() => EnterpriseHost;
}
