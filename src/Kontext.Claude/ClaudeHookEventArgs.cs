using Kontext.Domain;

namespace Kontext.Claude;

internal sealed class ClaudeHookEventArgs(ClaudeHookEvent hook) : EventArgs
{
    public ClaudeHookEvent Hook { get; } = hook;
}
