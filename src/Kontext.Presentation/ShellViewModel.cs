using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Presentation;

public sealed partial class ShellViewModel(
    IGitHubService gitHub,
    IGitHubDeviceLogin deviceLogin,
    IGitHubTokenStore tokenStore,
    IEnterpriseServerPrompt enterprisePrompt,
    IStartupRegistration startup,
    IAutoCleanupSetting autoCleanup,
    WorkspaceViewModel workspace) : ObservableObject
{
    public WorkspaceViewModel Workspace { get; } = workspace;

    [ObservableProperty]
    public partial bool LaunchOnStartup { get; set; }

    /// <summary>Whether the sessions of closed issues are cleared in the background. It is saved as soon as it changes.</summary>
    public bool CleanUpAutomatically
    {
        get => autoCleanup.IsEnabled;
        set
        {
            if (value == autoCleanup.IsEnabled)
            {
                return;
            }

            autoCleanup.IsEnabled = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(IsSignedOut), nameof(ShowSignIn), nameof(SignedInText), nameof(GitHubStatusText), nameof(HasGitHubAccount), nameof(HasNoGitHubAccount))]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial GitHubUser? User { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(IsSignedOut), nameof(ShowSignIn), nameof(EnterpriseSignedInText), nameof(EnterpriseStatusText), nameof(HasEnterpriseAccount), nameof(HasNoEnterpriseAccount))]
    [NotifyCanExecuteChangedFor(nameof(SignInEnterpriseCommand))]
    public partial GitHubUser? EnterpriseUser { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignIn))]
    public partial bool IsStarting { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand), nameof(SignInEnterpriseCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeviceCode))]
    public partial string? UserCode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceCodePrompt), nameof(VerificationLinkText))]
    public partial Uri? VerificationUrl { get; set; }

    public bool HasDeviceCode => !string.IsNullOrEmpty(UserCode);

    public string DeviceCodePrompt => VerificationUrl is null ? string.Empty : $"Enter this code on {VerificationUrl.Authority}:";

    public string VerificationLinkText => VerificationUrl is null ? string.Empty : $"Open {VerificationUrl.Authority}{VerificationUrl.AbsolutePath}";

    public bool IsSignedIn => User is not null || EnterpriseUser is not null;

    public bool IsSignedOut => !IsSignedIn;

    public bool ShowSignIn => IsSignedOut && !IsStarting;

    public bool HasGitHubAccount => User is not null;

    public bool HasNoGitHubAccount => User is null;

    public bool HasEnterpriseAccount => EnterpriseUser is not null;

    public bool HasNoEnterpriseAccount => EnterpriseUser is null;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string SignedInText => User is null ? string.Empty : $"Signed in as {User.Name ?? User.Login} (@{User.Login})";

    public string EnterpriseSignedInText => EnterpriseUser is null ? string.Empty : $"Signed in to {EnterpriseUser.Host} as {EnterpriseUser.Name ?? EnterpriseUser.Login} (@{EnterpriseUser.Login})";

    public string GitHubStatusText => User is null ? "Not signed in" : SignedInText;

    public string EnterpriseStatusText => EnterpriseUser is null ? "Not signed in" : EnterpriseSignedInText;

    [RelayCommand]
    private async Task RefreshStartupAsync()
    {
        try
        {
            LaunchOnStartup = await startup.GetStateAsync() == StartupState.Enabled;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LaunchOnStartup = false;
            ErrorMessage = $"Could not read the launch on startup setting: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SetLaunchOnStartupAsync(bool enabled)
    {
        if (enabled == LaunchOnStartup)
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            var state = await startup.SetEnabledAsync(enabled);
            LaunchOnStartup = state == StartupState.Enabled;
            if (!enabled && LaunchOnStartup)
            {
                ErrorMessage = "Windows kept launch on startup enabled. Turn it off in Windows Settings > Apps > Startup, or ask your administrator if it is managed by policy.";
            }
            else if (enabled && !LaunchOnStartup)
            {
                ErrorMessage = state switch
                {
                    StartupState.DisabledByUser => "Windows blocked launch on startup because it was turned off for Kontext. Turn it on in Windows Settings > Apps > Startup.",
                    StartupState.DisabledByPolicy => "Launch on startup is disabled by your organization's policy.",
                    _ => "Windows did not enable launch on startup.",
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RefreshStartupAsync();
            ErrorMessage = $"Could not change launch on startup: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            var signedIn = await SignInWithSavedTokenAsync(GitHubHosts.Default, "GitHub");

            if (tokenStore.FindEnterpriseHost() is { } enterpriseHost)
            {
                signedIn |= await SignInWithSavedTokenAsync(enterpriseHost, $"GitHub Enterprise ({enterpriseHost})");
            }

            if (signedIn)
            {
                Workspace.Sidebar.RefreshCommand.Execute(null);
            }
        }
        finally
        {
            IsStarting = false;
        }
    }

    private async Task<bool> SignInWithSavedTokenAsync(string host, string accountName)
    {
        var savedToken = tokenStore.Load(host);
        if (savedToken is null)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            ShowUser(await gitHub.AuthenticateAsync(host, savedToken));
            return true;
        }
        catch (AuthenticationFailedException)
        {
            tokenStore.Clear(host);
            AddError($"Your saved {accountName} sign-in is no longer valid. Please sign in again.");
            return false;
        }
        catch (KontextException ex)
        {
            AddError(ex.Message);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var challenge = await deviceLogin.StartAsync(GitHubServer.GitHub, cancellationToken);
            UserCode = challenge.UserCode;
            VerificationUrl = challenge.VerificationUri;

            var token = await deviceLogin.WaitForAccessTokenAsync(challenge, cancellationToken);
            await CompleteSignInAsync(GitHubHosts.Default, token, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (KontextException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            UserCode = null;
            VerificationUrl = null;
            IsBusy = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanSignInEnterprise))]
    private async Task SignInEnterpriseAsync(CancellationToken cancellationToken)
    {
        var credentials = await enterprisePrompt.AskAsync();
        if (credentials is null)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await CompleteSignInAsync(credentials.Server.Host, credentials.Token, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (KontextException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CompleteSignInAsync(string host, string token, CancellationToken cancellationToken)
    {
        ShowUser(await gitHub.AuthenticateAsync(host, token, cancellationToken));
        tokenStore.Save(host, token);
        Workspace.Sidebar.RefreshCommand.Execute(null);
    }

    [RelayCommand]
    private void CancelSignIn()
    {
        SignInCancelCommand.Execute(null);
        SignInEnterpriseCancelCommand.Execute(null);
    }

    [RelayCommand]
    private void SignOut() => SignOutOf(GitHubHosts.Default, SignInCancelCommand);

    [RelayCommand]
    private void SignOutEnterprise()
    {
        var host = EnterpriseUser?.Host ?? tokenStore.FindEnterpriseHost();
        if (host is null)
        {
            SignInEnterpriseCancelCommand.Execute(null);
            return;
        }

        SignOutOf(host, SignInEnterpriseCancelCommand);
    }

    private void SignOutOf(string host, System.Windows.Input.ICommand cancelSignIn)
    {
        cancelSignIn.Execute(null);
        UserCode = null;
        VerificationUrl = null;
        gitHub.SignOut(host);
        tokenStore.Clear(host);
        ErrorMessage = null;

        if (GitHubHosts.IsDefault(host))
        {
            User = null;
        }
        else
        {
            EnterpriseUser = null;
        }

        if (IsSignedIn)
        {
            Workspace.Sidebar.RefreshCommand.Execute(null);
            return;
        }

        Workspace.Sidebar.Clear();
        Workspace.IsSettingsOpen = false;
    }

    partial void OnUserChanged(GitHubUser? value) => Workspace.Sidebar.SetAccountLogin(GitHubHosts.Default, value?.Login);

    partial void OnEnterpriseUserChanged(GitHubUser? oldValue, GitHubUser? newValue)
    {
        if ((newValue ?? oldValue) is { } user)
        {
            Workspace.Sidebar.SetAccountLogin(user.Host, newValue?.Login);
        }
    }

    private void ShowUser(GitHubUser user)
    {
        if (GitHubHosts.IsDefault(user.Host))
        {
            User = user;
        }
        else
        {
            EnterpriseUser = user;
        }
    }

    private void AddError(string message) => ErrorMessage = string.IsNullOrEmpty(ErrorMessage) ? message : $"{ErrorMessage} {message}";

    private bool CanSignIn() => !IsBusy && User is null;

    private bool CanSignInEnterprise() => !IsBusy && EnterpriseUser is null;
}
