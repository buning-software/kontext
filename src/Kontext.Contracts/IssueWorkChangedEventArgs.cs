using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public sealed class IssueWorkChangedEventArgs(IssueKey issue, IssueWorkState state) : EventArgs
{
    public IssueKey Issue { get; } = issue;

    public IssueWorkState State { get; } = state;
}
