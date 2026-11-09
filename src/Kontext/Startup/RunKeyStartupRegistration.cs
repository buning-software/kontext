using Kontext.Contracts;
using Kontext.Domain;
using Microsoft.Win32;

namespace Kontext.Startup;

internal sealed class RunKeyStartupRegistration(string valueName) : IStartupRegistration
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static string? ExePath
    {
        get
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) || string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase)
                ? null
                : path;
        }
    }

    public Task<StartupState> GetStateAsync() => Task.FromResult(Read());

    public Task<StartupState> SetEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            var exe = ExePath ?? throw new InvalidOperationException("Kontext is running through the dotnet host, so there is no Kontext.exe to start at sign-in.");
            using (var run = Registry.CurrentUser.CreateSubKey(RunPath))
            {
                run.SetValue(valueName, $"\"{exe}\" {StartupActivation.Flag}", RegistryValueKind.String);
            }

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedPath, writable: true);
            approved?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        else
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunPath, writable: true);
            run?.DeleteValue(valueName, throwOnMissingValue: false);
        }

        return Task.FromResult(Read());
    }

    private StartupState Read()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunPath);
        if (run?.GetValue(valueName) is not string { Length: > 0 })
        {
            return StartupState.Disabled;
        }

        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedPath);
        var flags = approved?.GetValue(valueName) as byte[];
        return flags is { Length: > 0 } && (flags[0] & 1) == 1 ? StartupState.DisabledByUser : StartupState.Enabled;
    }
}
