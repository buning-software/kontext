using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.TestSupport;

public sealed class FakeStartupRegistration : IStartupRegistration
{
    public StartupState State { get; set; } = StartupState.Disabled;

    public StartupState? ResultOfEnabling { get; set; }

    public StartupState? ResultOfDisabling { get; set; }

    public Exception? FailsWith { get; set; }

    public int SetCalls { get; private set; }

    public Task<StartupState> GetStateAsync() => Task.FromResult(State);

    public Task<StartupState> SetEnabledAsync(bool enabled)
    {
        SetCalls++;
        if (FailsWith is not null)
        {
            return Task.FromException<StartupState>(FailsWith);
        }

        State = enabled ? ResultOfEnabling ?? StartupState.Enabled : ResultOfDisabling ?? StartupState.Disabled;
        return Task.FromResult(State);
    }
}
