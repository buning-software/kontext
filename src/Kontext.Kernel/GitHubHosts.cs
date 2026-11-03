namespace Kontext.Kernel;

public static class GitHubHosts
{
    public const string Default = "github.com";

    public static bool IsDefault(string? host) => string.Equals(host, Default, StringComparison.OrdinalIgnoreCase);
}
