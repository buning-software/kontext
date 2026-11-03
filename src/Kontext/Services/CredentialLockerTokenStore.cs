using Kontext.Contracts;
using Kontext.Kernel;
using Windows.Security.Credentials;

namespace Kontext.Services;

internal sealed class CredentialLockerTokenStore : IGitHubTokenStore
{
    private const string Resource = "Kontext/GitHub";
    private const string UserName = "github-oauth-token";
    private const string LegacyUserName = "personal-access-token";
    private const string EnterpriseResource = "Kontext/GitHub-Enterprise";
    private const int ElementNotFound = unchecked((int)0x80070490);

    private readonly PasswordVault _vault = new();

    public string? Load(string host)
    {
        RemoveLegacy();

        var credential = GitHubHosts.IsDefault(host) ? Find(Resource, UserName) : Find(EnterpriseResource, host);
        credential?.RetrievePassword();
        return credential?.Password;
    }

    public void Save(string host, string token)
    {
        if (GitHubHosts.IsDefault(host))
        {
            _vault.Add(new PasswordCredential(Resource, UserName, token));
            return;
        }

        // Only one enterprise account is kept, so a new one replaces whatever was saved before.
        foreach (var existing in FindAll(EnterpriseResource))
        {
            _vault.Remove(existing);
        }

        _vault.Add(new PasswordCredential(EnterpriseResource, host, token));
    }

    public void Clear(string host)
    {
        var credential = GitHubHosts.IsDefault(host) ? Find(Resource, UserName) : Find(EnterpriseResource, host);
        if (credential is not null)
        {
            _vault.Remove(credential);
        }
    }

    public string? FindEnterpriseHost() => FindAll(EnterpriseResource).FirstOrDefault()?.UserName;

    private void RemoveLegacy()
    {
        var legacy = Find(Resource, LegacyUserName);
        if (legacy is not null)
        {
            _vault.Remove(legacy);
        }
    }

    private PasswordCredential? Find(string resource, string userName)
    {
        try
        {
            return _vault.Retrieve(resource, userName);
        }
        catch (Exception ex) when (ex.HResult == ElementNotFound)
        {
            return null;
        }
    }

    private List<PasswordCredential> FindAll(string resource)
    {
        try
        {
            return [.. _vault.FindAllByResource(resource)];
        }
        catch (Exception ex) when (ex.HResult == ElementNotFound)
        {
            return [];
        }
    }
}
