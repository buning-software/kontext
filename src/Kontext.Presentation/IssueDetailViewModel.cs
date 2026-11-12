using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class IssueDetailViewModel : ObservableObject, IDisposable
{
    private const string NoDescription = "_No description provided._";

    private readonly IIssueSessionCoordinator _coordinator;
    private readonly IIssueSessionClearer _clearer;
    private readonly IConfirmation _confirmation;
    private readonly IWorkspaceOpener _opener;
    private readonly IIssueSessionRegistry _registry;
    private readonly SelectionAutoStart _autoStart;

    public IssueDetailViewModel(
        IIssueSessionCoordinator coordinator,
        IIssueSessionClearer clearer,
        IConfirmation confirmation,
        IWorkspaceOpener opener,
        IIssueSessionRegistry registry,
        SelectionAutoStart autoStart,
        GitChangesViewModel gitChanges)
    {
        _coordinator = coordinator;
        _clearer = clearer;
        _confirmation = confirmation;
        _opener = opener;
        _registry = registry;
        _autoStart = autoStart;
        GitChanges = gitChanges;

        _registry.Changed += OnSessionChanged;
        _autoStart.PropertyChanged += OnAutoStartChanged;
    }

    [ObservableProperty]
    public partial IssueItemViewModel? Item { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTerminalTab), nameof(ShowGitTab), nameof(ShowIssueTab), nameof(VisibleSession))]
    public partial DetailTab SelectedTab { get; set; }

    public bool ShowTerminalTab => SelectedTab == DetailTab.Terminal;

    public bool ShowGitTab => SelectedTab == DetailTab.Git;

    public bool ShowIssueTab => SelectedTab == DetailTab.Issue;

    public GitChangesViewModel GitChanges { get; }

    public bool HasSelection => Item is not null;

    public bool HasNoSelection => Item is null;

    public string Title => Item?.Title ?? string.Empty;

    public string RepositoryName => Item?.Issue.Repository.DisplayName ?? string.Empty;

    public string NumberText => Item?.NumberText ?? string.Empty;

    public Uri? Url => Item?.Issue.Url;

    public string LabelsText => Item is null ? string.Empty : string.Join(" · ", Item.Issue.Labels);

    public bool HasLabels => Item?.Issue.Labels.Count > 0;

    public string Subtitle => Item?.Subtitle ?? string.Empty;

    public string Body => string.IsNullOrWhiteSpace(Item?.Issue.Body) ? NoDescription : Item!.Issue.Body!;

    public IssueWorkStatus Status => Item?.Status ?? IssueWorkStatus.NotStarted;

    public string StatusText => Item?.StatusText ?? string.Empty;

    public string? StatusMessage => Item?.State.Message;

    public string? BranchName => Item?.State.BranchName;

    public string? WorktreePath => Item?.State.WorktreePath;

    public bool HasWorkspace => !string.IsNullOrEmpty(WorktreePath);

    public bool CanClearSession => Item is { Status: IssueWorkStatus.Done or IssueWorkStatus.Failed } item && item.Issue.IsClosed && HasWorkspace;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleSession))]
    public partial bool IsConfirmingClear { get; set; }

    /// <summary>
    /// True while another page (the settings or the general Claude terminal) covers the detail pane. The terminal is a native window that XAML cannot
    /// hide by collapsing its parent, so no session is shown then.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleSession))]
    public partial bool IsCovered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToolError))]
    public partial string? ToolError { get; set; }

    public bool HasToolError => !string.IsNullOrEmpty(ToolError);

    public ITerminalSession? Session => Item is null ? null : _registry.Find(Item.Key);

    public ITerminalSession? VisibleSession => ShowTerminalTab && !IsConfirmingClear && !IsCovered ? Session : null;

    public bool HasSession => Session is not null;

    public bool HasNoSession => Session is null;

    public bool IsPending => Item is not null && _autoStart.PendingIssue == Item.Key;

    public bool HasEnded => Item is { } item && (item.State.SessionEnded || item.Status == IssueWorkStatus.Failed);

    /// <summary>The line next to Resume. Without a session the reason is already the terminal placeholder, so it is not repeated here.</summary>
    public string EndedText => Item?.Status == IssueWorkStatus.Failed
        ? HasSession ? StatusMessage ?? "The session failed." : "Claude could not be started."
        : "Session ended.";

    public string TerminalPlaceholder => Item?.Status switch
    {
        _ when IsPending => "Starting Claude in a moment…",
        IssueWorkStatus.Preparing => "Preparing the workspace…",
        IssueWorkStatus.Done => "This issue is closed.",
        IssueWorkStatus.Failed => StatusMessage ?? "Claude could not be started.",
        _ => "Claude starts when you stay on this issue.",
    };

    public void Dispose()
    {
        _registry.Changed -= OnSessionChanged;
        _autoStart.PropertyChanged -= OnAutoStartChanged;
        UnsubscribeFrom(Item);
        GitChanges.Dispose();
    }

    partial void OnSelectedTabChanged(DetailTab value) => SyncGitChanges();

    partial void OnItemChanged(IssueItemViewModel? oldValue, IssueItemViewModel? newValue)
    {
        UnsubscribeFrom(oldValue);
        IsConfirmingClear = false;
        ToolError = null;

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnItemPropertyChanged;
        }

        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasEnded))]
    private async Task ResumeAsync()
    {
        if (Item is not null)
        {
            await _coordinator.StartOrResumeAsync(Item.Issue);
        }
    }

    [RelayCommand(CanExecute = nameof(HasWorkspace))]
    private void OpenIn(WorkspaceTool tool)
    {
        ToolError = null;

        try
        {
            _opener.Open(tool, WorktreePath!);
        }
        catch (ExternalToolException ex)
        {
            ToolError = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearSession))]
    private async Task ClearSessionAsync()
    {
        if (Item is not { } item)
        {
            return;
        }

        IsConfirmingClear = true;
        bool confirmed;
        try
        {
            confirmed = await _confirmation.ConfirmAsync(
                "Clear session",
                $"The worktree {WorktreePath} and the branch {BranchName} will be removed.{Environment.NewLine}{Environment.NewLine}Uncommitted changes and unpushed commits are lost.",
                "Clear session");
        }
        finally
        {
            IsConfirmingClear = false;
        }

        if (confirmed)
        {
            await _clearer.ClearAsync(item.Issue);
        }
    }

    private void UnsubscribeFrom(IssueItemViewModel? item)
    {
        if (item is not null)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnAutoStartChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnSessionChanged(object? sender, IssueSessionChangedEventArgs e)
    {
        if (Item is not null && e.Issue == Item.Key)
        {
            Refresh();
        }
    }

    // The Git tab only exists for an issue with a worktree, and the changes are polled only while that tab is shown.
    private void SyncGitChanges()
    {
        if (ShowGitTab && !HasWorkspace)
        {
            SelectedTab = DetailTab.Terminal;
            return;
        }

        GitChanges.Issue = Item?.Issue;
        GitChanges.WorktreePath = WorktreePath;
        GitChanges.IsActive = ShowGitTab;
    }

    private void Refresh()
    {
        SyncGitChanges();
        OnPropertyChanged(string.Empty);
        ResumeCommand.NotifyCanExecuteChanged();
        OpenInCommand.NotifyCanExecuteChanged();
        ClearSessionCommand.NotifyCanExecuteChanged();
    }
}
