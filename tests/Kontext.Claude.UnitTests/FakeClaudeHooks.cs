using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Claude.UnitTests;

internal sealed class FakeClaudeHooks : IClaudeHooks
{
    public const string DefaultSettings = "{\"hooks\":{}}";

    public string? Settings { get; set; } = DefaultSettings;

    public List<TerminalSessionId> Asked { get; } = [];

    public string? SettingsFor(TerminalSessionId session)
    {
        Asked.Add(session);
        return Settings;
    }
}
