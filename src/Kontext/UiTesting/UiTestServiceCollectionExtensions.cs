using Kontext.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kontext.UiTesting;

internal static class UiTestServiceCollectionExtensions
{
    public const string EnvironmentVariable = "KONTEXT_UI_TEST";

    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>Swaps GitHub and session start-up for fakes so UI tests need no sign-in, network or git.</summary>
    public static IServiceCollection AddKontextUiTestFakes(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IGitHubTokenStore, UiTestTokenStore>());
        services.Replace(ServiceDescriptor.Singleton<IGitHubService, UiTestGitHubService>());
        services.Replace(ServiceDescriptor.Singleton<IIssueSessionCoordinator, UiTestSessionCoordinator>());

        // One instance serves both contracts, so a commit is seen by the next change listing.
        var workspace = new UiTestWorkspace();
        services.Replace(ServiceDescriptor.Singleton<IWorkspaceChanges>(workspace));
        services.Replace(ServiceDescriptor.Singleton<IWorkspaceCommitter>(workspace));
        return services;
    }
}
