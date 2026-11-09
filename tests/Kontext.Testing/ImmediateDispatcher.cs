using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class ImmediateDispatcher : IUiDispatcher
{
    public bool HasThreadAccess => true;

    public Task<T> RunAsync<T>(Func<T> work) => Task.FromResult(work());

    public Task RunAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
