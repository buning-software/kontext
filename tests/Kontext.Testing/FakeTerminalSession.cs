using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeTerminalSession(TerminalSessionId id) : ITerminalSession
{
    private readonly TaskCompletionSource<int?> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TerminalSessionId Id { get; } = id;

    public bool HasExited => _exited.Task.IsCompleted;

    public Task<int?> Exited => _exited.Task;

    public List<string> Written { get; } = [];

    public void Write(string text) => Written.Add(text);

    public void End(int? exitCode) => _exited.SetResult(exitCode);

    public void Crash(Exception error) => _exited.SetException(error);
}
