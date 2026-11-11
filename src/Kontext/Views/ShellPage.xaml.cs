using System.ComponentModel;
using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
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

    private void CopyDetails_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.Workspace.Errors.Details is not { Length: > 0 } details)
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(details);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.VerificationUrl) && ViewModel.VerificationUrl is { } url && LinkPolicy.IsSafeToOpen(url))
        {
            await Launcher.LaunchUriAsync(url);
        }
    }
}
