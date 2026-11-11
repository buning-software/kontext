using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Kernel;

namespace Kontext.Presentation;

/// <summary>
/// The one error bar across the bottom of the window. Every source reports into it and the newest error replaces the
/// current one. A poll error that was closed stays hidden while the same message repeats; an action error is simply
/// replaced by the next failing action.
/// </summary>
public sealed partial class ErrorBarViewModel : ObservableObject
{
    private readonly Dictionary<string, string> _dismissedPolls = new(StringComparer.Ordinal);
    private string? _pollSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetails))]
    public partial string? Details { get; private set; }

    [ObservableProperty]
    public partial bool IsDetailsOpen { get; set; }

    public bool IsOpen
    {
        get => !string.IsNullOrEmpty(Message);
        set
        {
            if (!value)
            {
                Dismiss();
            }
        }
    }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    /// <summary>Shows a failed action (commit, push). It is shown again for every failure, even with the same message.</summary>
    public void ReportAction(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Show(exception);
        _pollSource = null;
    }

    /// <summary>Shows a failed poll or load. Once closed, the same message from the same source stays hidden until it changes or a poll succeeds.</summary>
    public void ReportPoll(string source, Exception exception)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentNullException.ThrowIfNull(exception);

        if (_dismissedPolls.TryGetValue(source, out var dismissed) && dismissed == exception.Message)
        {
            return;
        }

        _dismissedPolls.Remove(source);
        Show(exception);
        _pollSource = source;
    }

    /// <summary>A poll succeeded: its error goes away, and a closed error from it may be shown again.</summary>
    public void ClearPoll(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        _dismissedPolls.Remove(source);
        if (_pollSource == source)
        {
            Reset();
        }
    }

    /// <summary>The user starts another action, so the previous action's error is stale.</summary>
    public void ClearAction()
    {
        if (_pollSource is null)
        {
            Reset();
        }
    }

    [RelayCommand]
    public void Dismiss()
    {
        if (_pollSource is { } source && Message is { } message)
        {
            _dismissedPolls[source] = message;
        }

        Reset();
    }

    private void Show(Exception exception)
    {
        Message = exception.Message;
        Details = (exception as KontextException)?.Details;
        IsDetailsOpen = false;
    }

    private void Reset()
    {
        Message = null;
        Details = null;
        IsDetailsOpen = false;
        _pollSource = null;
    }
}
