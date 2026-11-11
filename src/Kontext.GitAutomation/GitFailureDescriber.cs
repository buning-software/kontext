namespace Kontext.GitAutomation;

/// <summary>Turns a failed git run into a plain-language message. The raw output stays available as the exception's details.</summary>
internal static class GitFailureDescriber
{
    public const string Fallback = "Git command failed.";

    public static string Describe(string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardError);

        if (ContainsAny(standardError, "non-fast-forward", "[rejected]", "fetch first", "failed to push some refs"))
        {
            return "Push rejected: the remote branch has commits you do not have. Pull or rebase, then push again.";
        }

        if (ContainsAny(standardError, "Authentication failed", "could not read Username", "could not read Password", "Permission denied", "returned error: 403", "returned error: 401", "Invalid username or token"))
        {
            return "Authentication failed. Sign in to GitHub again and retry.";
        }

        if (ContainsAny(standardError, "No configured push destination", "has no upstream branch", "No such remote", "does not appear to be a git repository", "No remote repository specified", "no upstream configured"))
        {
            return "No remote or upstream is configured for this branch.";
        }

        if (ContainsAny(standardError, "Could not resolve host", "Failed to connect", "Could not connect to server", "Connection timed out", "Connection refused", "Network is unreachable", "Operation timed out", "unable to access", "Connection reset"))
        {
            return "Could not reach the git host. Check your network connection and try again.";
        }

        if (ContainsAny(standardError, "git was not found", "'git' is not recognized", "git: command not found"))
        {
            return NotInstalled;
        }

        return Fallback;
    }

    public const string NotInstalled = "Git is not installed or could not be found. Install Git for Windows and make sure it is on your PATH.";

    private static bool ContainsAny(string text, params string[] values) => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
