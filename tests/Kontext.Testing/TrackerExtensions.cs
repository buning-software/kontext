using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;
using NUnit.Framework;

namespace Kontext.Testing;

public static class TrackerExtensions
{
    public static List<IssueWorkStatus> Record(this IIssueWorkTracker tracker, IssueKey issue)
    {
        var statuses = new List<IssueWorkStatus>();
        tracker.Changed += (_, e) =>
        {
            if (e.Issue == issue)
            {
                statuses.Add(e.State.Status);
            }
        };

        return statuses;
    }
}
