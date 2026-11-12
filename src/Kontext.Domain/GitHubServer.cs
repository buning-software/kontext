using Kontext.Kernel;

namespace Kontext.Domain;

public sealed record GitHubServer(string Host, string? ClientId = null)
{
    public static GitHubServer GitHub { get; } = new(GitHubHosts.Default);

    public bool IsEnterprise => !GitHubHosts.IsDefault(Host);

    public Uri WebUri => new($"https://{Host}/");

    public Uri ApiUri => GitHubHosts.IsDefault(Host)
        ? new Uri("https://api.github.com/")
        : Host.EndsWith(".ghe.com", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"https://api.{Host}/")
            : new Uri($"https://{Host}/api/v3/");

    public Uri GraphQlUri => GitHubHosts.IsDefault(Host)
        ? new Uri("https://api.github.com/graphql")
        : Host.EndsWith(".ghe.com", StringComparison.OrdinalIgnoreCase)
            ? new Uri($"https://api.{Host}/graphql")
            : new Uri($"https://{Host}/api/graphql");

    public static bool TryCreate(string? address, out GitHubServer? server, out string? error)
    {
        server = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            error = "Enter the address of your GitHub Enterprise server.";
            return false;
        }

        var text = address.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.Host.Contains('.', StringComparison.Ordinal)
            || uri.AbsolutePath.Trim('/').Length > 0)
        {
            error = "That is not a valid server address. Use something like github.yourcompany.com.";
            return false;
        }

        var host = uri.Authority.ToLowerInvariant();
        if (GitHubHosts.IsDefault(host) || host == "www.github.com")
        {
            error = "That is regular GitHub. Use \"Sign in with GitHub\" for github.com.";
            return false;
        }

        server = new GitHubServer(host);
        error = null;
        return true;
    }
}
