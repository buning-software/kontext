using Kontext.Testing;

namespace Kontext.Presentation.UnitTests;

public abstract class ShellViewModelFixture
{
    private SelectionAutoStart _autoStart = null!;
    private IssueDetailViewModel _detail = null!;
    private GeneralSessionViewModel _general = null!;

    protected FakeGitHubService GitHub { get; private set; } = null!;

    protected FakeGitHubDeviceLogin DeviceLogin { get; private set; } = null!;

    protected FakeTokenStore Tokens { get; private set; } = null!;

    protected FakeEnterprisePrompt EnterprisePrompt { get; private set; } = null!;

    protected FakeStartupRegistration Startup { get; private set; } = null!;

    protected InMemoryAutoCleanupSetting AutoCleanupSetting { get; private set; } = null!;

    protected IssueSidebarViewModel Sidebar { get; private set; } = null!;

    protected ShellViewModel Shell { get; private set; } = null!;

    [SetUp]
    public void CreateShell() => Build(savedToken: null);

    [TearDown]
    public void Dispose()
    {
        _detail.Dispose();
        _general.Dispose();
        Sidebar.Dispose();
        _autoStart.Dispose();
    }

    protected ErrorBarViewModel Errors { get; private set; } = null!;

    protected void Build(string? savedToken, string? savedEnterpriseToken = null)
    {
        Errors = new ErrorBarViewModel();
        var tracker = new InMemoryIssueWorkTracker();
        var coordinator = new FakeCoordinator();
        GitHub = new FakeGitHubService { Issues = [Issues.Create(4, "Add notifications", "acme/memento")] };
        Tokens = new FakeTokenStore(savedToken, savedEnterpriseToken);
        EnterprisePrompt = new FakeEnterprisePrompt();
        DeviceLogin = new FakeGitHubDeviceLogin();
        Startup = new FakeStartupRegistration();
        AutoCleanupSetting = new InMemoryAutoCleanupSetting();
        _autoStart = new SelectionAutoStart(coordinator, tracker, new InMemoryIssueSessionRegistry(), TimeProvider.System, AutoStartOptions.Default);
        Sidebar = new IssueSidebarViewModel(GitHub, tracker, new FakeExistingWorkspaceFinder(), new WorkspaceIssues(GitHub), new InMemoryHiddenRepositoryStore(), new InMemoryCollapsedRepositoryStore(), new FakeSessionStore(), new FakeTranscripts(), new AutoCleanup(new FakeSessionClearer(), new FakeWorkspaceChanges(), new FakeWorkspaceCommitter(), new InMemoryAutoCleanupSetting { IsEnabled = false }), Errors, TimeProvider.System);
        _detail = new IssueDetailViewModel(coordinator, new FakeSessionClearer(), new FakeConfirmation(), new FakeWorkspaceOpener(), new InMemoryIssueSessionRegistry(), _autoStart, new GitChangesViewModel(new FakeWorkspaceChanges(), new FakeWorkspaceCommitter(), new FakeIssueBranchLinker(), Errors, TimeProvider.System, GitChangesOptions.Default), new PullRequestStatusViewModel(new FakeGitHubService(), TimeProvider.System, PullRequestStatusOptions.Default));
        _general = new GeneralSessionViewModel(new FakeGeneralSessionCoordinator());
        Shell = new ShellViewModel(GitHub, DeviceLogin, Tokens, EnterprisePrompt, Startup, AutoCleanupSetting, new WorkspaceViewModel(Sidebar, _detail, _general, _autoStart, Errors));
    }
}
