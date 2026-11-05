using Kontext.Kernel;

namespace Kontext.Claude;

/// <summary>The session to resume is already open in another Claude (another terminal, or another Kontext).</summary>
public class ClaudeSessionInUseException : KontextException
{
    public ClaudeSessionInUseException()
        : this("This session is already open in another Claude window. Close it there, then resume it here.")
    {
    }

    public ClaudeSessionInUseException(string message)
        : base(message)
    {
    }

    public ClaudeSessionInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
