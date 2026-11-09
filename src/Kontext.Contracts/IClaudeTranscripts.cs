using Kontext.Domain;

namespace Kontext.Contracts;

/// <summary>Tells whether a Claude session has a conversation on disk. A session that was only started, with nothing typed yet, has none.</summary>
public interface IClaudeTranscripts
{
    bool Exists(TerminalSessionId sessionId);
}
