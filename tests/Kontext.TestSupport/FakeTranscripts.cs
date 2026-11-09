using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.TestSupport;

public sealed class FakeTranscripts : IClaudeTranscripts
{
    public bool Present { get; set; } = true;

    public bool Exists(TerminalSessionId sessionId) => Present;
}
