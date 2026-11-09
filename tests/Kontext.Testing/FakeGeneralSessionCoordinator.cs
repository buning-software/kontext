using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Testing;

public sealed class FakeGeneralSessionCoordinator : IGeneralSessionCoordinator
{
    public ITerminalSession? Current { get; set; }

    public string? StartError { get; set; }

    public int Starts { get; private set; }

    public event EventHandler? Changed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Starts++;
        Current = new FakeTerminalSession(TerminalSessionId.New());
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
