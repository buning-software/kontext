using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public sealed class IssueSessionChangedEventArgs(IssueKey issue, ITerminalSession session) : EventArgs
{
    public IssueKey Issue { get; } = issue;

    public ITerminalSession Session { get; } = session;
}
