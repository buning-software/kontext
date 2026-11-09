using Kontext.Contracts;
using Kontext.Domain;
using Windows.ApplicationModel;

namespace Kontext.Startup;

internal sealed class MsixStartupRegistration : IStartupRegistration
{
    public const string TaskId = "KontextStartup";

    public async Task<StartupState> GetStateAsync() => Map((await StartupTask.GetAsync(TaskId)).State);

    public async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(TaskId);
        if (enabled)
        {
            return Map(await task.RequestEnableAsync());
        }

        task.Disable();
        return Map(task.State);
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => StartupState.Enabled,
        StartupTaskState.DisabledByUser => StartupState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupState.DisabledByPolicy,
        _ => StartupState.Disabled,
    };
}
