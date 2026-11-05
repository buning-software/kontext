using Kontext.Claude;
using Kontext.Contracts;
using Kontext.ExternalTools;
using Kontext.GitAutomation;
using Kontext.GitHub;
using Kontext.Kernel;
using Kontext.Presentation;
using Kontext.Services;
using Kontext.Sessions;
using Kontext.Terminal;
using Kontext.TerminalHost;
using Kontext.Tray;
using Kontext.UiTesting;
using Kontext.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace Kontext;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontext(this IServiceCollection services, IConfiguration configuration)
    {
        var overridden = Environment.GetEnvironmentVariable("KONTEXT_DATA_DIR");
        var dataRoot = string.IsNullOrWhiteSpace(overridden)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kontext")
            : overridden;

        services.AddSingleton(new RefreshOptions(RefreshInterval(configuration)));
        services
            .AddKontextGitHub(new GitHubOptions(configuration["GitHub:ClientId"]))
            .AddKontextGitAutomation(new GitAutomationOptions(
                StateDirectory: dataRoot,
                WorktreesRoot: Path.Combine(dataRoot, "worktrees"),
                ClonesRoot: Path.Combine(dataRoot, "repos")))
            .AddKontextTerminalHost()
            .AddKontextClaude(new ClaudeOptions(
                ClaudeConfigDirectory(),
                PermissionMode: configuration["Claude:PermissionMode"] is { Length: > 0 } mode ? mode : "plan"))
            .AddKontextExternalTools()
            .AddKontextSessions(new SessionsOptions(dataRoot))
            .AddKontextPresentation();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();
        services.AddSingleton<XamlRootProvider>();
        services.AddSingleton<IConfirmation, ContentDialogConfirmation>();
        services.AddSingleton<IGitHubTokenStore, CredentialLockerTokenStore>();
        services.AddSingleton<IEnterpriseServerPrompt, ContentDialogEnterprisePrompt>();

        services.AddSingleton<IUiDispatcher>(_ => new DispatcherQueueDispatcher(DispatcherQueue.GetForCurrentThread()));

        services.AddSingleton<TerminalControlHost>();
        services.AddSingleton<ITerminalSessionService, EasyTerminalSessionService>();

        services.AddSingleton(new AppIconOptions(Path.Combine(AppContext.BaseDirectory, "Assets", "Kontext.ico")));
        services.AddSingleton<NotifyIcon>();
        services.AddSingleton<MenuTheme>();
        services.AddSingleton<TrayContextMenu>();
        services.AddSingleton<TrayHost>();

        services.AddTransient<ShellPage>();
        services.AddSingleton<MainWindow>();

        if (UiTestServiceCollectionExtensions.IsEnabled)
        {
            services.AddKontextUiTestFakes();
        }

        return services;
    }

    private static TimeSpan RefreshInterval(IConfiguration configuration) =>
        int.TryParse(configuration["Refresh:IntervalSeconds"], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : RefreshOptions.Default.Interval;

    private static string ClaudeConfigDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configured;
    }
}
