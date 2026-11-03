using Kontext.Domain;

namespace Kontext.Contracts;

public interface IGitHubDeviceLogin
{
    Task<DeviceCodeChallenge> StartAsync(GitHubServer? server = null, CancellationToken cancellationToken = default);

    Task<string> WaitForAccessTokenAsync(DeviceCodeChallenge challenge, CancellationToken cancellationToken = default);
}
