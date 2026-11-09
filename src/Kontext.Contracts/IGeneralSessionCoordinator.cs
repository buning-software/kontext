namespace Kontext.Contracts;

public interface IGeneralSessionCoordinator
{
    ITerminalSession? Current { get; }

    string? StartError { get; }

    event EventHandler? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);
}
