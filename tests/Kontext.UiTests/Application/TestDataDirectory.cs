using Microsoft.Win32;
using WinUia.Models;

namespace Kontext.UiTests.Application;

/// <summary>
/// A throwaway data folder for one launch: Kontext reads and writes it instead of %LOCALAPPDATA%\Kontext. The launch also
/// switches Kontext to UI test mode, which serves the issues in <see cref="FakeGitHub"/> and never starts Claude, and
/// registers autostart under its own Run-key value so a test never touches the real "Kontext" entry.
/// </summary>
public sealed class TestDataDirectory : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"kontext-ui-tests-{Guid.NewGuid():N}");

    public string StartupValueName { get; } = $"Kontext.UiTest.{Guid.NewGuid():N}";

    public IReadOnlyDictionary<string, string?> Environment => new Dictionary<string, string?>
    {
        ["KONTEXT_DATA_DIR"] = Root,
        ["KONTEXT_UI_TEST"] = "1",
        ["KONTEXT_STARTUP_VALUE_NAME"] = StartupValueName,
    };

    public AppLaunchOptions LaunchOptions => new() { Environment = new Dictionary<string, string?>(Environment) };

    public string? RegisteredStartupCommand
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return run?.GetValue(StartupValueName) as string;
        }
    }

    public void RegisterStartup(string command)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        run.SetValue(StartupValueName, command);
    }

    public void Dispose()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
        {
            run?.DeleteValue(StartupValueName, throwOnMissingValue: false);
        }

        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Kontext can hold a file for a moment after it closes; a temp folder is not worth failing a test.
        }
    }
}
