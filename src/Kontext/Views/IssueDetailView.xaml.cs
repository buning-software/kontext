using System.ComponentModel;
using Kontext.Domain;
using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class IssueDetailView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(IssueDetailViewModel),
        typeof(IssueDetailView),
        new PropertyMetadata(null, (view, args) => ((IssueDetailView)view).OnViewModelChanged((IssueDetailViewModel?)args.OldValue, (IssueDetailViewModel?)args.NewValue)));

    public static readonly DependencyProperty ControlHostProperty = DependencyProperty.Register(
        nameof(ControlHost),
        typeof(TerminalControlHost),
        typeof(IssueDetailView),
        new PropertyMetadata(null, (view, _) => ((IssueDetailView)view).Bindings?.Update()));

    public IssueDetailView() => InitializeComponent();

    public IssueDetailViewModel? ViewModel
    {
        get => (IssueDetailViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TerminalControlHost? ControlHost
    {
        get => (TerminalControlHost?)GetValue(ControlHostProperty);
        set => SetValue(ControlHostProperty, value);
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e) => OpenIn(WorkspaceTool.Explorer);

    private void OpenInTerminal_Click(object sender, RoutedEventArgs e) => OpenIn(WorkspaceTool.Terminal);

    private void OpenSolution_Click(object sender, RoutedEventArgs e) => OpenIn(WorkspaceTool.Solution);

    private void OpenIn(WorkspaceTool tool) => ViewModel?.OpenInCommand.Execute(tool);

    private void OnViewModelChanged(IssueDetailViewModel? oldValue, IssueDetailViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnViewModelPropertyChanged;
        }

        Bindings?.Update();
        SelectTabItem();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IssueDetailViewModel.SelectedTab))
        {
            SelectTabItem();
        }
    }

    // The view model can move the selection itself (for example back to Terminal when the Git tab disappears).
    private void SelectTabItem()
    {
        var item = ViewModel?.SelectedTab switch
        {
            DetailTab.Git => GitTab,
            DetailTab.Issue => IssueTab,
            _ => TerminalTab,
        };

        if (!ReferenceEquals(Tabs.SelectedItem, item))
        {
            Tabs.SelectedItem = item;
        }
    }

    private void Tabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ViewModel is not null)
        {
            ViewModel.SelectedTab = ReferenceEquals(sender.SelectedItem, GitTab)
                ? DetailTab.Git
                : ReferenceEquals(sender.SelectedItem, IssueTab) ? DetailTab.Issue : DetailTab.Terminal;
        }
    }
}
