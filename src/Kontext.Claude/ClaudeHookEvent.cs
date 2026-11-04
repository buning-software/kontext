using Kontext.Domain;

namespace Kontext.Claude;

/// <param name="Session">The id Kontext gave the terminal session; it is part of the hook address.</param>
/// <param name="ClaudeSession">The id Claude itself reports. Resuming gives a conversation a new id, so it can differ from the one it was started with.</param>
internal sealed record ClaudeHookEvent(TerminalSessionId Session, string Name, string? NotificationType = null, Guid? ClaudeSession = null);

internal sealed class ClaudeHookEventArgs(ClaudeHookEvent hook) : EventArgs
{
    public ClaudeHookEvent Hook { get; } = hook;
}
