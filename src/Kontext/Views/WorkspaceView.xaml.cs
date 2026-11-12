using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Kontext.Views;

public sealed partial class WorkspaceView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(WorkspaceView),
        new PropertyMetadata(null, (view, _) => ((WorkspaceView)view).Bindings?.Update()));

    public static readonly DependencyProperty ControlHostProperty = DependencyProperty.Register(
        nameof(ControlHost),
        typeof(TerminalControlHost),
        typeof(WorkspaceView),
        new PropertyMetadata(null, (view, _) => ((WorkspaceView)view).Bindings?.Update()));

    public WorkspaceView() => InitializeComponent();

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Workspace.Errors.Details is not { Length: > 0 } details)
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(details);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TerminalControlHost? ControlHost
    {
        get => (TerminalControlHost?)GetValue(ControlHostProperty);
        set => SetValue(ControlHostProperty, value);
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked && ViewModel is not null)
        {
            ViewModel.Workspace.IsSettingsOpen = true;
        }
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (args.IsSettingsSelected)
        {
            ViewModel.Workspace.IsSettingsOpen = true;
        }
        else if (args.SelectedItem is GeneralSessionViewModel)
        {
            ViewModel.Workspace.OpenGeneral();
        }
        else if (args.SelectedItem is IssueItemViewModel issue)
        {
            ViewModel.Workspace.OpenIssue();
            ViewModel.Workspace.Sidebar.SelectedItem = issue;
        }
    }
}
