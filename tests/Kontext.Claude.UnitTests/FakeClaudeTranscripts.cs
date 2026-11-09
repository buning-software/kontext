using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Claude.UnitTests;

internal sealed class FakeClaudeTranscripts : IClaudeTranscripts
{
    public HashSet<TerminalSessionId> Saved { get; } = [];

    public bool Exists(TerminalSessionId sessionId) => Saved.Contains(sessionId);
}
