using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Claude;

internal sealed class ClaudeActivityReporter(IIssueSessionRegistry registry, IIssueWorkTracker tracker, ISessionStore sessions, IUiDispatcher ui)
{
    public void OnHook(ClaudeHookEvent hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        _ = ui.RunAsync(() => Apply(hook));
    }

    private void Apply(ClaudeHookEvent hook)
    {
        if (registry.IssueOf(hook.Session) is not { } issue)
        {
            return;
        }

        RememberSession(issue, hook.ClaudeSession);

        var state = tracker.StateOf(issue);

        if (state.SessionEnded)
        {
            return;
        }

        if (ClaudeActivityMapper.Next(state.Status, hook) is { } next && next != state.Status)
        {
            tracker.Report(issue, state with { Status = next });
        }
    }

    // Resuming a conversation gives Claude a new session id with the history copied into it. Keeping the id the
    // launch used would bring the next resume back to the copy as it was, losing everything said since.
    private void RememberSession(IssueKey issue, Guid? claudeSession)
    {
        if (claudeSession is not { } id || sessions.Find(issue) is not { } stored || stored.SessionId.Value == id)
        {
            return;
        }

        sessions.Save(stored with { SessionId = new TerminalSessionId(id) });
    }
}
