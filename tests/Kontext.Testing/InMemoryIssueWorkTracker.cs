using System.Collections.Concurrent;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class InMemoryIssueWorkTracker : IIssueWorkTracker
{
    private readonly ConcurrentDictionary<IssueKey, IssueWorkState> _states = new();

    public event EventHandler<IssueWorkChangedEventArgs>? Changed;

    public IssueWorkState StateOf(IssueKey issue) => _states.GetValueOrDefault(issue, IssueWorkState.NotStarted);

    public void Report(IssueKey issue, IssueWorkState state)
    {
        _states[issue] = state;
        Changed?.Invoke(this, new IssueWorkChangedEventArgs(issue, state));
    }
}
