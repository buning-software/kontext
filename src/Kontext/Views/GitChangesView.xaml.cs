using System.ComponentModel;
using System.Text;
using Kontext.Domain;
using Kontext.Presentation;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Kontext.Views;

public sealed partial class GitChangesView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(GitChangesViewModel),
        typeof(GitChangesView),
        new PropertyMetadata(null, (view, args) => ((GitChangesView)view).OnViewModelChanged((GitChangesViewModel?)args.OldValue, (GitChangesViewModel?)args.NewValue)));

    private bool _syncing;

    public GitChangesView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyDisabledCommitColors();
        ActualThemeChanged += (_, _) => ApplyDisabledCommitColors();
    }

    public GitChangesViewModel? ViewModel
    {
        get => (GitChangesViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // The disabled Commit button is the accent colour washed out towards grey, not plain grey and not a faded blue.
    private void ApplyDisabledCommitColors()
    {
        var accent = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        var grey = ActualTheme == ElementTheme.Dark ? (byte)0x5A : (byte)0xB4;
        byte Mix(byte channel) => (byte)((channel * 0.45) + (grey * 0.55));
        var washedOut = Windows.UI.Color.FromArgb(0xFF, Mix(accent.R), Mix(accent.G), Mix(accent.B));

        CommitButton.Resources["AccentButtonBackgroundDisabled"] = new SolidColorBrush(washedOut);
        CommitButton.Resources["AccentButtonBorderBrushDisabled"] = new SolidColorBrush(washedOut);
        CommitButton.Resources["AccentButtonForegroundDisabled"] = new SolidColorBrush(Colors.White) { Opacity = 0.75 };
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
        SyncDiff();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(GitChangesViewModel.Rows) or nameof(GitChangesViewModel.SelectedChange))
        {
            SyncList();
        }

        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(GitChangesViewModel.SelectedDiff))
        {
            SyncDiff();
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
        => DiffContent.MinWidth = Math.Max(0, e.NewSize.Width - 20);

    // The unified diff: all code in one RichTextBlock (one line break per diff line) so a selection can span lines and
    // copies only code; numbers and signs in unselectable columns; one background row per line. All use DiffLineHeight.
    private void SyncDiff()
    {
        var lines = ViewModel?.SelectedDiff?.Lines ?? [];
        var oldNumbers = new StringBuilder();
        var newNumbers = new StringBuilder();
        var signs = new StringBuilder();
        var code = new Paragraph();

        DiffBackgrounds.Children.Clear();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (index > 0)
            {
                oldNumbers.Append('\n');
                newNumbers.Append('\n');
                signs.Append('\n');
                code.Inlines.Add(new LineBreak());
            }

            oldNumbers.Append(GitViewFormat.Number(line.OldNumber));
            newNumbers.Append(GitViewFormat.Number(line.NewNumber));
            signs.Append(line.Kind switch
            {
                DiffLineKind.Added => '+',
                DiffLineKind.Removed => '−',
                _ => ' ',
            });

            var text = new Run { Text = line.Text };
            if (line.Kind == DiffLineKind.HunkHeader && Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var secondary))
            {
                text.Foreground = (Brush)secondary;
            }

            code.Inlines.Add(text);
            DiffBackgrounds.Children.Add(new Border { Style = RowStyle(line.Kind) });
        }

        DiffOldNumbers.Text = oldNumbers.ToString();
        DiffNewNumbers.Text = newNumbers.ToString();
        DiffSigns.Text = signs.ToString();
        DiffText.Blocks.Clear();
        DiffText.Blocks.Add(code);
    }

    private Style RowStyle(DiffLineKind kind) => (Style)Resources[kind switch
    {
        DiffLineKind.Added => "DiffAddedRowStyle",
        DiffLineKind.Removed => "DiffRemovedRowStyle",
        DiffLineKind.HunkHeader => "DiffHunkRowStyle",
        _ => "DiffRowStyle",
    }];
}
