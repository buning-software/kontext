using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Claude.UnitTests;

internal sealed class FakeClaudeLiveSessions : IClaudeLiveSessions
{
    public Dictionary<TerminalSessionId, int> Running { get; } = [];

    public int? FindRunningProcess(TerminalSessionId sessionId) => Running.TryGetValue(sessionId, out var pid) ? pid : null;
}
