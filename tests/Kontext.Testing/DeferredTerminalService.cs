using Kontext.Contracts;
using Kontext.Domain;

namespace Kontext.Testing;

public sealed class DeferredTerminalService : ITerminalSessionService
{
    private readonly TaskCompletionSource<ITerminalSession> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Starts { get; private set; }

    public Task<ITerminalSession> StartAsync(TerminalSessionStart start, CancellationToken cancellationToken = default)
    {
        Starts++;
        return _pending.Task;
    }

    public void Complete(TerminalSessionId id) => _pending.SetResult(new FakeTerminalSession(id));

    public ITerminalSession? Find(TerminalSessionId id) => null;

    public void Close(TerminalSessionId id)
    {
    }

    public void Dispose()
    {
    }
}
