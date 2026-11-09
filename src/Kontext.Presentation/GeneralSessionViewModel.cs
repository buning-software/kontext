using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;

namespace Kontext.Presentation;

public sealed partial class GeneralSessionViewModel : ObservableObject, IDisposable
{
    private readonly IGeneralSessionCoordinator _coordinator;

    public GeneralSessionViewModel(IGeneralSessionCoordinator coordinator)
    {
        _coordinator = coordinator;
        _coordinator.Changed += OnCoordinatorChanged;
    }

    public string Title { get; } = "Claude";

    /// <summary>
    /// True while another page (an issue or the settings) covers the detail pane. The terminal is a native window that XAML
    /// cannot hide by collapsing its parent, so no session is shown then.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleSession))]
    public partial bool IsCovered { get; set; } = true;

    public ITerminalSession? Session => _coordinator.Current;

    public ITerminalSession? VisibleSession => IsCovered ? null : Session;

    public bool HasSession => Session is not null;

    public bool HasNoSession => Session is null;

    public bool HasEnded => Session is { HasExited: true } || (Session is null && _coordinator.StartError is not null);

    public string EndedText => Session is null ? "Claude could not be started." : "Session ended.";

    public string Placeholder => _coordinator.StartError ?? "Starting Claude…";

    public void Dispose() => _coordinator.Changed -= OnCoordinatorChanged;

    public void Open()
    {
        IsCovered = false;

        if (Session is null)
        {
            _ = StartAsync();
        }
    }

    [RelayCommand]
    private Task StartNewSessionAsync() => StartAsync();

    private async Task StartAsync()
    {
        await _coordinator.StartAsync();
        Refresh();
    }

    private void OnCoordinatorChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(Session));
        OnPropertyChanged(nameof(VisibleSession));
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(HasNoSession));
        OnPropertyChanged(nameof(HasEnded));
        OnPropertyChanged(nameof(EndedText));
        OnPropertyChanged(nameof(Placeholder));
    }
}
