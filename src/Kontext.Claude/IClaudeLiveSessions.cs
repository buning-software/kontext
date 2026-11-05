using Kontext.Domain;

namespace Kontext.Claude;

/// <summary>The Claude sessions that are open right now, in any terminal or in another Kontext.</summary>
internal interface IClaudeLiveSessions
{
    /// <summary>The process id of the running Claude that has <paramref name="sessionId"/> open, or null when none has.</summary>
    int? FindRunningProcess(TerminalSessionId sessionId);
}
