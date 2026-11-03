using System.Text.RegularExpressions;
using Kontext.Kernel;

namespace Kontext.GitAutomation;

internal static class GitRemoteUrlParser
{
    public static RepositoryRef? Parse(string remoteUrl, string host = GitHubHosts.Default)
    {
        var escaped = Regex.Escape(host);
        var text = remoteUrl.Trim();

        var match = Regex.Match(
            text,
            $@"^(?:https?|git|ssh)://(?:[^@/]+@)?(?:www\.)?{escaped}(?::\d+)?/(?<owner>[^/]+)/(?<name>[^/]+?)(?:\.git)?/?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            match = Regex.Match(
                text,
                $@"^(?:[^@/]+@)?{escaped}:(?<owner>[^/]+)/(?<name>[^/]+?)(?:\.git)?/?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return match.Success ? new RepositoryRef(match.Groups["owner"].Value, match.Groups["name"].Value, Host: host) : null;
    }
}
