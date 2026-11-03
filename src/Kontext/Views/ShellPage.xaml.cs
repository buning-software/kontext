using System.ComponentModel;
using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Kontext.Views;

public sealed partial class ShellPage : Page
{
    public ShellPage(ShellViewModel viewModel, TerminalControlHost controlHost)
    {
        ViewModel = viewModel;
        ControlHost = controlHost;
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += (_, _) => ViewModel.InitializeCommand.Execute(null);
    }

    public ShellViewModel ViewModel { get; }

    public TerminalControlHost ControlHost { get; }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.VerificationUrl) && ViewModel.VerificationUrl is { } url && LinkPolicy.IsSafeToOpen(url))
        {
            await Launcher.LaunchUriAsync(url);
        }
    }
}
