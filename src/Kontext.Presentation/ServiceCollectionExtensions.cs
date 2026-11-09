using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kontext.Presentation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKontextPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<AppLifetime>();
        services.AddSingleton(AutoStartOptions.Default);
        services.TryAddSingleton(RefreshOptions.Default);
        services.AddSingleton<IWorkspaceIssues, WorkspaceIssues>();
        services.AddSingleton<SelectionAutoStart>();

        services.AddTransient<IssueSidebarViewModel>();
        services.AddSingleton(GitChangesOptions.Default);

        services.AddTransient<GitChangesViewModel>();
        services.AddTransient<IssueDetailViewModel>();
        services.AddTransient<GeneralSessionViewModel>();
        services.AddTransient<WorkspaceViewModel>();
        services.AddTransient<ShellViewModel>();

        return services;
    }
}
