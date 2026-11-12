using System.Collections.Concurrent;
using Octokit;

namespace Kontext.GitHub;

/// <summary>The signed-in GitHub client of every host, shared by the services that call GitHub.</summary>
internal sealed class GitHubClients
{
    private readonly ConcurrentDictionary<string, GitHubClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string host, GitHubClient client) => _clients[host] = client;

    public void Remove(string host) => _clients.TryRemove(host, out _);

    public KeyValuePair<string, GitHubClient>[] All() => _clients.ToArray();

    public GitHubClient For(string host) =>
        _clients.TryGetValue(host, out var client) ? client : throw new GitHubException($"You are not signed in to {host}.");

    public string TokenFor(string host) => For(host).Credentials.GetToken();
}
