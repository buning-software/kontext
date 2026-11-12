using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.GitHub;

internal sealed class GitHubDeviceLogin(GitHubOptions options, GitHubFormPoster poster, TimeProvider time) : IGitHubDeviceLogin
{
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string NotConfigured = "GitHub sign-in is not configured. Set GitHub:ClientId to the client id of a GitHub OAuth App with device flow enabled, in appsettings.local.json next to Kontext.exe, then restart Kontext.";

    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    public async Task<DeviceCodeChallenge> StartAsync(GitHubServer? server = null, CancellationToken cancellationToken = default)
    {
        var host = server?.Host ?? GitHubHosts.Default;
        var clientId = RequireClientId(server?.ClientId);
        var body = await poster.PostAsync(DeviceCodeUrl(host), new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = "repo workflow" }, cancellationToken);

        var challenge = DeviceFlowResponses.ParseDeviceCode(body, time.GetUtcNow());
        if (challenge.VerificationUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(challenge.VerificationUri.Authority, host, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitHubException($"{Label(host)} sent an unexpected verification address, so the sign-in was stopped.");
        }

        return challenge with { Host = host, ClientId = clientId };
    }

    public async Task<string> WaitForAccessTokenAsync(DeviceCodeChallenge challenge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var clientId = RequireClientId(challenge.ClientId);
        var accessTokenUrl = AccessTokenUrl(challenge.Host);

        var interval = challenge.PollInterval;
        while (true)
        {
            await Task.Delay(interval, time, cancellationToken);
            if (time.GetUtcNow() >= challenge.ExpiresAt)
            {
                throw Expired();
            }

            var form = new Dictionary<string, string> { ["client_id"] = clientId, ["device_code"] = challenge.DeviceCode, ["grant_type"] = DeviceGrantType };
            var result = DeviceFlowResponses.ParseTokenPoll(await poster.PostAsync(accessTokenUrl, form, cancellationToken));
            switch (result.Status)
            {
                case DeviceTokenStatus.Granted:
                    return result.Token!;
                case DeviceTokenStatus.Pending:
                    break;
                case DeviceTokenStatus.SlowDown:
                    interval = result.IntervalSeconds is > 0 and var seconds ? TimeSpan.FromSeconds(seconds) : interval + SlowDownStep;
                    break;
                case DeviceTokenStatus.Expired:
                    throw Expired();
                case DeviceTokenStatus.Denied:
                    throw new AuthenticationFailedException($"Sign-in was cancelled on {Label(challenge.Host)}.");
                default:
                    throw new GitHubException($"{result.Message} Check the client id and that device flow is enabled for the GitHub OAuth App.");
            }
        }
    }

    private static string Label(string host) => GitHubHosts.IsDefault(host) ? "GitHub" : host;

    private static string DeviceCodeUrl(string host) => $"https://{host}/login/device/code";

    private static string AccessTokenUrl(string host) => $"https://{host}/login/oauth/access_token";

    private static AuthenticationFailedException Expired() => new("The sign-in code expired. Please try again.");

    private string RequireClientId(string? explicitClientId)
    {
        var clientId = string.IsNullOrWhiteSpace(explicitClientId) ? options.ClientId : explicitClientId;
        return string.IsNullOrWhiteSpace(clientId) ? throw new GitHubException(NotConfigured) : clientId;
    }
}
