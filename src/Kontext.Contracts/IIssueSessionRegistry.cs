using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Contracts;

public interface IIssueSessionRegistry
{
    event EventHandler<IssueSessionChangedEventArgs>? Changed;

    ITerminalSession? Find(IssueKey issue);

    IssueKey? IssueOf(TerminalSessionId session);

    void Register(IssueKey issue, ITerminalSession session);

    void Remove(IssueKey issue);
}
