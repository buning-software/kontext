using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

internal sealed class GeneralSessionCoordinator(
    ITerminalSessionService terminals,
    IClaudeLauncher claude,
    IUiDispatcher ui) : IGeneralSessionCoordinator
{
    private bool _starting;

    public ITerminalSession? Current { get; private set; }

    public string? StartError { get; private set; }

    public event EventHandler? Changed;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_starting || Current is { HasExited: false })
        {
            return;
        }

        _starting = true;
        try
        {
            if (Current is { } ended)
            {
                Current = null;
                terminals.Close(ended.Id);
            }

            var launch = claude.PrepareGeneral(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            var session = await terminals.StartAsync(new TerminalSessionStart(launch.SessionId, launch.Options), cancellationToken);

            Current = session;
            StartError = null;
            Changed?.Invoke(this, EventArgs.Empty);

            _ = ReportWhenEndedAsync(session);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            StartError = ex.Message;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _starting = false;
        }
    }

    private async Task ReportWhenEndedAsync(ITerminalSession session)
    {
        try
        {
            await session.Exited;
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
        }

        await ui.RunAsync(() =>
        {
            if (ReferenceEquals(Current, session))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        });
    }
}
