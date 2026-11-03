using System.Text;
using Kontext.Contracts;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal sealed class GitHubTokenAuthentication(IGitHubTokenStore tokens) : IGitAuthentication
{
    public IReadOnlyDictionary<string, string> EnvironmentVariables()
    {
        var headers = SignedInHosts()
            .Select(host => (Host: host, Token: tokens.Load(host)))
            .Where(account => !string.IsNullOrWhiteSpace(account.Token))
            .ToList();

        if (headers.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var environment = new Dictionary<string, string> { ["GIT_CONFIG_COUNT"] = (headers.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) };

        for (var i = 0; i < headers.Count; i++)
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{headers[i].Token}"));
            environment[$"GIT_CONFIG_KEY_{i}"] = $"http.https://{headers[i].Host}/.extraheader";
            environment[$"GIT_CONFIG_VALUE_{i}"] = $"AUTHORIZATION: basic {basic}";
        }

        environment[$"GIT_CONFIG_KEY_{headers.Count}"] = "credential.helper";
        environment[$"GIT_CONFIG_VALUE_{headers.Count}"] = string.Empty;

        return environment;
    }

    private IEnumerable<string> SignedInHosts()
    {
        yield return GitHubHosts.Default;

        if (tokens.FindEnterpriseHost() is { } enterprise)
        {
            yield return enterprise;
        }
    }
}
