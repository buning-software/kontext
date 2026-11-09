using System.ComponentModel;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class SettingsView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(SettingsView),
        new PropertyMetadata(null, (view, _) => ((SettingsView)view).OnViewModelChanged()));

    private ShellViewModel? _attached;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private void OnViewModelChanged()
    {
        Detach();
        Bindings?.Update();
        if (IsLoaded)
        {
            Attach();
        }
    }

    private void Attach()
    {
        if (ViewModel is not { } viewModel || ReferenceEquals(_attached, viewModel))
        {
            return;
        }

        _attached = viewModel;
        viewModel.Workspace.PropertyChanged += OnWorkspaceChanged;
        viewModel.RefreshStartupCommand.Execute(null);
    }

    private void Detach()
    {
        _attached?.Workspace.PropertyChanged -= OnWorkspaceChanged;
        _attached = null;
    }

    private void OnWorkspaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceViewModel.IsSettingsOpen) && ViewModel is { Workspace.IsSettingsOpen: true } viewModel)
        {
            viewModel.RefreshStartupCommand.Execute(null);
        }
    }

    private async void OnLaunchOnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel || LaunchOnStartupToggle.IsOn == viewModel.LaunchOnStartup)
        {
            return;
        }

        LaunchOnStartupToggle.IsEnabled = false;
        try
        {
            await viewModel.SetLaunchOnStartupCommand.ExecuteAsync(LaunchOnStartupToggle.IsOn);
            LaunchOnStartupToggle.IsOn = viewModel.LaunchOnStartup;
        }
        finally
        {
            LaunchOnStartupToggle.IsEnabled = true;
        }
    }

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public string AppVersion { get; } = $"Version {typeof(SettingsView).Assembly.GetName().Version?.ToString(3)}";
}
