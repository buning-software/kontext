using Kontext.Domain;
using Kontext.Kernel;
using Kontext.TestSupport;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class ShellViewModelEnterpriseTests : ShellViewModelFixture
{
    private async Task SignInToGitHubAsync()
    {
        await Shell.InitializeCommand.ExecuteAsync(null);
        await Shell.SignInCommand.ExecuteAsync(null);
    }

    [Test]
    public async Task AsksForTheServerAndTokenThenSignsInWithThemAndSavesTheTokenUnderThatHost()
    {
        await Shell.InitializeCommand.ExecuteAsync(null);

        await Shell.SignInEnterpriseCommand.ExecuteAsync(null);

        Assert.That(EnterprisePrompt.Asked, Is.EqualTo(1));
        Assert.That(DeviceLogin.Started, Is.Zero, "enterprise uses a personal access token, not the device flow");
        Assert.That(Shell.HasDeviceCode, Is.False);
        Assert.That(GitHub.HostsTried, Is.EqualTo(Expect.Seq("github.corp.com")));
        Assert.That(GitHub.TokensTried, Is.EqualTo(Expect.Seq("ghp_corporate")));
        Assert.That(Shell.IsBusy, Is.False);
        Assert.That(Tokens.SavedEnterprise, Is.EqualTo("ghp_corporate"));
        Assert.That(Tokens.EnterpriseHost, Is.EqualTo("github.corp.com"));
        Assert.That(Tokens.Saved, Is.Null, "the github.com token is untouched");
        Assert.That(Shell.EnterpriseUser?.Host, Is.EqualTo("github.corp.com"));
        Assert.That(Shell.User, Is.Null);
        Assert.That(Shell.IsSignedIn, Is.True);
        Assert.That(Shell.EnterpriseSignedInText, Does.Contain("github.corp.com").And.Contain("corpuser"));
    }

    [Test]
    public async Task DoesNothingWhenTheServerPromptIsCancelled()
    {
        await Shell.InitializeCommand.ExecuteAsync(null);
        EnterprisePrompt.Answer = null;

        await Shell.SignInEnterpriseCommand.ExecuteAsync(null);

        Assert.That(DeviceLogin.Started, Is.Zero);
        Assert.That(Shell.IsSignedIn, Is.False);
        Assert.That(Shell.IsBusy, Is.False);
        Assert.That(Shell.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task CanHoldOneGitHubAccountAndOneEnterpriseAccountAtTheSameTime()
    {
        await SignInToGitHubAsync();

        await Shell.SignInEnterpriseCommand.ExecuteAsync(null);

        Assert.That(Shell.User?.Login, Is.EqualTo("octocat"));
        Assert.That(Shell.EnterpriseUser?.Login, Is.EqualTo("corpuser"));
        Assert.That(Shell.HasGitHubAccount, Is.True);
        Assert.That(Shell.HasEnterpriseAccount, Is.True);
        Assert.That(Tokens.Saved, Is.Not.Null);
        Assert.That(Tokens.SavedEnterprise, Is.Not.Null);
    }

    [Test]
    public async Task OffersASignInOnlyForTheAccountThatIsNotSignedInYet()
    {
        await SignInToGitHubAsync();

        Assert.That(Shell.SignInCommand.CanExecute(null), Is.False, "github.com is already signed in");
        Assert.That(Shell.SignInEnterpriseCommand.CanExecute(null), Is.True);

        await Shell.SignInEnterpriseCommand.ExecuteAsync(null);

        Assert.That(Shell.SignInEnterpriseCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task ShowsTheEnterpriseErrorAndSavesNothingWhenTheServerRejectsTheSignIn()
    {
        await Shell.InitializeCommand.ExecuteAsync(null);
        GitHub.AuthenticationFailsWith = new AuthenticationFailedException("github.corp.com rejected this sign-in.");

        await Shell.SignInEnterpriseCommand.ExecuteAsync(null);

        Assert.That(Shell.ErrorMessage, Is.EqualTo("github.corp.com rejected this sign-in."));
        Assert.That(Tokens.SavedEnterprise, Is.Null);
        Assert.That(Shell.IsSignedIn, Is.False);
    }

    [Test]
    public async Task SignsBackInToBothAccountsFromTheirSavedTokensAndLoadsTheIssuesOnce()
    {
        Build(savedToken: "ghp_saved", savedEnterpriseToken: "ghe_saved");

        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(GitHub.HostsTried, Is.EqualTo(Expect.Seq("github.com", "github.corp.com")));
        Assert.That(Shell.User, Is.Not.Null);
        Assert.That(Shell.EnterpriseUser, Is.Not.Null);
        await AsyncAssert.EventuallyAsync(() => GitHub.Calls == 1, "a single issue load for both accounts");
    }

    [Test]
    public async Task SignsInWithJustTheEnterpriseTokenWhenThereIsNoGitHubToken()
    {
        Build(savedToken: null, savedEnterpriseToken: "ghe_saved");

        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(Shell.User, Is.Null);
        Assert.That(Shell.EnterpriseUser, Is.Not.Null);
        Assert.That(Shell.IsSignedIn, Is.True);
        Assert.That(Shell.ShowSignIn, Is.False);
    }

    [Test]
    public async Task ClearsARevokedEnterpriseTokenButKeepsTheGitHubAccount()
    {
        Build(savedToken: "ghp_saved", savedEnterpriseToken: "ghe_revoked");
        GitHub.OnAuthenticate = token =>
        {
            if (token == "ghe_revoked")
            {
                GitHub.AuthenticationFailsWith = new AuthenticationFailedException("rejected");
            }
        };

        await Shell.InitializeCommand.ExecuteAsync(null);

        Assert.That(Tokens.SavedEnterprise, Is.Null);
        Assert.That(Tokens.Saved, Is.EqualTo("ghp_saved"));
        Assert.That(Shell.User, Is.Not.Null);
        Assert.That(Shell.EnterpriseUser, Is.Null);
        Assert.That(Shell.ErrorMessage, Does.Contain("GitHub Enterprise").And.Contain("no longer valid"));
    }

    [Test]
    public async Task SigningOutOfTheEnterpriseAccountKeepsTheGitHubAccountAndReloadsTheIssues()
    {
        Build(savedToken: "ghp_saved", savedEnterpriseToken: "ghe_saved");
        await Shell.InitializeCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => GitHub.Calls == 1, "the first load");

        Shell.SignOutEnterpriseCommand.Execute(null);

        Assert.That(GitHub.SignedOutHosts, Is.EqualTo(Expect.Seq("github.corp.com")));
        Assert.That(Tokens.SavedEnterprise, Is.Null);
        Assert.That(Tokens.Saved, Is.EqualTo("ghp_saved"));
        Assert.That(Shell.EnterpriseUser, Is.Null);
        Assert.That(Shell.User, Is.Not.Null);
        Assert.That(Shell.IsSignedIn, Is.True);
        await AsyncAssert.EventuallyAsync(() => GitHub.Calls == 2, "the issues to reload without the enterprise account");
    }

    [Test]
    public async Task SigningOutOfTheGitHubAccountKeepsTheEnterpriseAccount()
    {
        Build(savedToken: "ghp_saved", savedEnterpriseToken: "ghe_saved");
        await Shell.InitializeCommand.ExecuteAsync(null);

        Shell.SignOutCommand.Execute(null);

        Assert.That(GitHub.SignedOutHosts, Is.EqualTo(Expect.Seq("github.com")));
        Assert.That(Tokens.Saved, Is.Null);
        Assert.That(Tokens.SavedEnterprise, Is.EqualTo("ghe_saved"));
        Assert.That(Shell.User, Is.Null);
        Assert.That(Shell.EnterpriseUser, Is.Not.Null);
        Assert.That(Shell.IsSignedIn, Is.True);
        Assert.That(Shell.ShowSignIn, Is.False);
    }

    [Test]
    public async Task ReturnsToTheSignInPageOnceTheLastAccountSignsOut()
    {
        Build(savedToken: null, savedEnterpriseToken: "ghe_saved");
        await Shell.InitializeCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => Sidebar.Groups.Count == 1, "the sidebar to load");
        Shell.Workspace.IsSettingsOpen = true;

        Shell.SignOutEnterpriseCommand.Execute(null);

        Assert.That(Shell.IsSignedIn, Is.False);
        Assert.That(Shell.ShowSignIn, Is.True);
        Assert.That(Sidebar.Groups, Is.Empty);
        Assert.That(Shell.Workspace.IsSettingsOpen, Is.False);
    }

    [Test]
    public async Task ShowsTheServerTheGitHubCodeMustBeEnteredOn()
    {
        await Shell.InitializeCommand.ExecuteAsync(null);
        DeviceLogin.Deferred = true;

        var signIn = Shell.SignInCommand.ExecuteAsync(null);
        await AsyncAssert.EventuallyAsync(() => DeviceLogin.Waited == 1, "the wait for approval to start");

        Assert.That(Shell.DeviceCodePrompt, Is.EqualTo("Enter this code on github.com:"));
        Assert.That(Shell.VerificationLinkText, Is.EqualTo("Open github.com/login/device"));

        Shell.CancelSignInCommand.Execute(null);
        await signIn;
        Assert.That(Shell.HasDeviceCode, Is.False);
        Assert.That(Shell.IsBusy, Is.False);
    }

    [Test]
    public void KeepsRepositoriesOfDifferentServersApart()
    {
        var publicRepository = new RepositoryRef("acme", "app");
        var corporate = new RepositoryRef("acme", "app", Host: "github.corp.com");

        Assert.That(new IssueKey(publicRepository, 1), Is.Not.EqualTo(new IssueKey(corporate, 1)));
        Assert.That(new IssueKey(publicRepository, 1).ToString(), Is.EqualTo("ACME/APP#1"), "github.com keys keep their saved format");
        Assert.That(new IssueKey(corporate, 1).ToString(), Is.EqualTo("GITHUB.CORP.COM/ACME/APP#1"));
        Assert.That(publicRepository.DisplayName, Is.EqualTo("acme/app"));
        Assert.That(corporate.DisplayName, Is.EqualTo("github.corp.com/acme/app"));
    }
}
