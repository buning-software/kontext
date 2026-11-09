using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeTerminalService : ITerminalSessionService
{
    public List<TerminalSessionStart> Starts { get; } = [];

    public Exception? FailWith { get; set; }

    public FakeTerminalSession? LastSession { get; private set; }

    public List<TerminalSessionId> Closed { get; } = [];

    public Task<ITerminalSession> StartAsync(TerminalSessionStart start, CancellationToken cancellationToken = default)
    {
        Starts.Add(start);

        if (FailWith is not null)
        {
            return Task.FromException<ITerminalSession>(FailWith);
        }

        LastSession = new FakeTerminalSession(start.Id);
        return Task.FromResult<ITerminalSession>(LastSession);
    }

    public ITerminalSession? Find(TerminalSessionId id) => LastSession is { } session && session.Id == id ? session : null;

    public void Close(TerminalSessionId id)
    {
        Closed.Add(id);

        if (LastSession is { HasExited: false } session && session.Id == id)
        {
            session.End(null);
        }
    }

    public void Dispose()
    {
    }
}
