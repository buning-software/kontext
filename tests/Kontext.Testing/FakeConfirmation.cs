using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Testing;

public sealed class FakeConfirmation : IConfirmation
{
    private TaskCompletionSource<bool>? _pending;

    public bool Answer { get; set; } = true;

    public bool Deferred { get; set; }

    public List<(string Title, string Message, string ConfirmText)> Asked { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Asked.Add((title, message, confirmText));

        if (!Deferred)
        {
            return Task.FromResult(Answer);
        }

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pending.Task;
    }

    public void Respond(bool answer) => _pending!.SetResult(answer);
}
