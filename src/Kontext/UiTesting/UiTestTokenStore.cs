using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.UiTesting;

internal sealed class UiTestTokenStore : IGitHubTokenStore
{
    public string? Load(string host) => host == GitHubHosts.Default ? "ui-test" : null;

    public void Save(string host, string token)
    {
    }

    public void Clear(string host)
    {
    }

    public string? FindEnterpriseHost() => null;
}
