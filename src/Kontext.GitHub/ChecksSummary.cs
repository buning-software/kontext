using Kontext.Domain;
using Octokit;

namespace Kontext.GitHub;

internal static class ChecksSummary
{
    public static ChecksState Of(IReadOnlyList<CheckRun> runs)
    {
        if (runs.Count == 0)
        {
            return ChecksState.None;
        }

        if (runs.Any(run => IsFailure(run)))
        {
            return ChecksState.Failing;
        }

        return runs.Any(run => run.Status.Value != CheckStatus.Completed) ? ChecksState.Pending : ChecksState.Passing;
    }

    private static bool IsFailure(CheckRun run) =>
        run.Conclusion?.Value is CheckConclusion.Failure or CheckConclusion.TimedOut or CheckConclusion.Cancelled or CheckConclusion.ActionRequired;
}
