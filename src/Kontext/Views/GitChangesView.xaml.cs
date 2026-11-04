using System.ComponentModel;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class GitChangesView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(GitChangesViewModel),
        typeof(GitChangesView),
        new PropertyMetadata(null, (view, args) => ((GitChangesView)view).OnViewModelChanged((GitChangesViewModel?)args.OldValue, (GitChangesViewModel?)args.NewValue)));

    private bool _syncing;

    public GitChangesView() => InitializeComponent();

    public GitChangesViewModel? ViewModel
    {
        get => (GitChangesViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged(GitChangesViewModel? oldValue, GitChangesViewModel? newValue)
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
        SyncList();
        SyncLayout();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(GitChangesViewModel.Rows) or nameof(GitChangesViewModel.SelectedChange))
        {
            SyncList();
        }

        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(GitChangesViewModel.IsSplitView))
        {
            SyncLayout();
        }
    }

    private void SyncLayout()
    {
        var item = ViewModel?.IsSplitView == true ? SplitItem : UnifiedItem;
        if (!ReferenceEquals(LayoutBar.SelectedItem, item))
        {
            _syncing = true;
            try
            {
                LayoutBar.SelectedItem = item;
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    private void LayoutBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!_syncing && ViewModel is not null)
        {
            ViewModel.IsSplitView = ReferenceEquals(sender.SelectedItem, SplitItem);
        }
    }

    // The list owns no state of its own: it shows the view model's rows and follows its selection.
    private void SyncList()
    {
        _syncing = true;
        try
        {
            if (ViewModel is not { } viewModel)
            {
                FileList.ItemsSource = null;
                return;
            }

            if (!ReferenceEquals(FileList.ItemsSource, viewModel.Rows))
            {
                FileList.ItemsSource = viewModel.Rows;
            }

            var selected = viewModel.SelectedChange;
            var row = selected is null ? null : viewModel.Rows.FirstOrDefault(candidate => candidate.Change == selected);
            if (!ReferenceEquals(FileList.SelectedItem, row))
            {
                FileList.SelectedItem = row;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && ViewModel is not null && FileList.SelectedItem is GitChangeRow { Change: { } change })
        {
            ViewModel.SelectedChange = change;
        }
    }

    // Rows must be at least as wide as the viewport so their backgrounds reach the right edge.
    private void DiffScroller_SizeChanged(object sender, SizeChangedEventArgs e)
        => DiffRepeater.MinWidth = Math.Max(0, e.NewSize.Width - 20);
}
