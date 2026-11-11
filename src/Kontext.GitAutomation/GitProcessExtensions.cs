namespace Kontext.GitAutomation;

public static class GitProcessExtensions
{
    public static async Task<GitResult> RunCheckedAsync(
        this IGitProcess git,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(git);

        var result = await git.RunAsync(workingDirectory, arguments, cancellationToken);
        if (!result.Succeeded)
        {
            throw Failure(result);
        }

        return result;
    }

    internal static GitAutomationException Failure(GitResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return new GitAutomationException(GitFailureDescriber.Describe(detail), result.ExitCode, detail);
    }
}
