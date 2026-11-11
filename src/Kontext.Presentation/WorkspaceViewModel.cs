using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kontext.Presentation;

public sealed partial class WorkspaceViewModel : ObservableObject
{
    private readonly SelectionAutoStart _autoStart;
    private readonly SidebarSeparator _separatorAbove = new();
    private readonly SidebarSeparator _separatorBelow = new();

    public WorkspaceViewModel(IssueSidebarViewModel sidebar, IssueDetailViewModel detail, GeneralSessionViewModel general, SelectionAutoStart autoStart, ErrorBarViewModel errors)
    {
        Sidebar = sidebar;
        Detail = detail;
        General = general;
        Errors = errors;
        _autoStart = autoStart;

        sidebar.PropertyChanged += OnSidebarPropertyChanged;
        sidebar.MenuGroups.CollectionChanged += OnMenuGroupsChanged;
        SyncMenuItems();
        ApplyPage();
    }

    public IssueSidebarViewModel Sidebar { get; }

    public IssueDetailViewModel Detail { get; }

    public GeneralSessionViewModel General { get; }

    /// <summary>The one error bar across the bottom of the window.</summary>
    public ErrorBarViewModel Errors { get; }

    /// <summary>What the sidebar menu binds to: a separator, the fixed general Claude entry, a separator, then a group for every repository.</summary>
    public ObservableCollection<object> MenuItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen), nameof(IsGeneralOpen), nameof(IsDetailVisible))]
    public partial WorkspacePage CurrentPage { get; set; }

    public bool IsSettingsOpen
    {
        get => CurrentPage == WorkspacePage.Settings;
        set
        {
            if (value)
            {
                CurrentPage = WorkspacePage.Settings;
            }
            else if (CurrentPage == WorkspacePage.Settings)
            {
                CurrentPage = WorkspacePage.Issue;
            }
        }
    }

    public bool IsGeneralOpen => CurrentPage == WorkspacePage.General;

    public bool IsDetailVisible => CurrentPage == WorkspacePage.Issue;

    public void OpenGeneral() => CurrentPage = WorkspacePage.General;

    public void OpenIssue() => CurrentPage = WorkspacePage.Issue;

    partial void OnCurrentPageChanged(WorkspacePage value) => ApplyPage();

    private void ApplyPage()
    {
        Detail.IsCovered = CurrentPage != WorkspacePage.Issue;
        General.IsCovered = CurrentPage != WorkspacePage.General;

        if (CurrentPage == WorkspacePage.General)
        {
            General.Open();
        }
    }

    private void OnMenuGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncMenuItems();

    private void SyncMenuItems() => CollectionSync.SyncInPlace(MenuItems, [_separatorAbove, General, _separatorBelow, .. Sidebar.MenuGroups]);

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IssueSidebarViewModel.SelectedItem))
        {
            var item = Sidebar.SelectedItem;
            if (item is not null && item.Key != Detail.Item?.Key)
            {
                CurrentPage = WorkspacePage.Issue;
            }

            Detail.Item = item;
            _autoStart.OnSelected(item?.Issue);
        }
    }
}
