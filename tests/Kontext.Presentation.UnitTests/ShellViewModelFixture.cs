using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

public abstract class ShellViewModelFixture
{
    private SelectionAutoStart _autoStart = null!;
    private IssueDetailViewModel _detail = null!;

    protected FakeGitHubService GitHub { get; private set; } = null!;

    protected FakeGitHubDeviceLogin DeviceLogin { get; private set; } = null!;

    protected FakeTokenStore Tokens { get; private set; } = null!;

    protected FakeEnterprisePrompt EnterprisePrompt { get; private set; } = null!;

    protected IssueSidebarViewModel Sidebar { get; private set; } = null!;

    protected ShellViewModel Shell { get; private set; } = null!;

    [SetUp]
    public void CreateShell() => Build(savedToken: null);

    [TearDown]
    public void Dispose()
    {
        _detail.Dispose();
        Sidebar.Dispose();
        _autoStart.Dispose();
    }

    protected void Build(string? savedToken, string? savedEnterpriseToken = null)
    {
        var tracker = new InMemoryIssueWorkTracker();
        var coordinator = new FakeCoordinator();
        GitHub = new FakeGitHubService { Issues = [Issues.Create(4, "Add notifications", "acme/memento")] };
        Tokens = new FakeTokenStore(savedToken, savedEnterpriseToken);
        EnterprisePrompt = new FakeEnterprisePrompt();
        DeviceLogin = new FakeGitHubDeviceLogin();
        _autoStart = new SelectionAutoStart(coordinator, tracker, new InMemoryIssueSessionRegistry(), TimeProvider.System, AutoStartOptions.Default);
        Sidebar = new IssueSidebarViewModel(GitHub, tracker, new FakeExistingWorkspaceFinder(), new WorkspaceIssues(GitHub), new InMemoryHiddenRepositoryStore(), new InMemoryCollapsedRepositoryStore(), new FakeSessionStore(), TimeProvider.System);
        _detail = new IssueDetailViewModel(coordinator, new FakeSessionClearer(), new FakeConfirmation(), new FakeWorkspaceOpener(), new InMemoryIssueSessionRegistry(), _autoStart, new GitChangesViewModel(new FakeWorkspaceChanges(), TimeProvider.System, GitChangesOptions.Default));
        Shell = new ShellViewModel(GitHub, DeviceLogin, Tokens, EnterprisePrompt, new WorkspaceViewModel(Sidebar, _detail, _autoStart));
    }
}
