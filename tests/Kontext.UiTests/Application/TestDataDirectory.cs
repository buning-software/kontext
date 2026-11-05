using WinUia.Models;

namespace Kontext.UiTests.Application;

/// <summary>
/// A throwaway data folder for one launch: Kontext reads and writes it instead of %LOCALAPPDATA%\Kontext. The launch also
/// switches Kontext to UI test mode, which serves the issues in <see cref="FakeGitHub"/> and never starts Claude.
/// </summary>
public sealed class TestDataDirectory : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"kontext-ui-tests-{Guid.NewGuid():N}");

    public AppLaunchOptions LaunchOptions => new()
    {
        Environment = new Dictionary<string, string?>
        {
            ["KONTEXT_DATA_DIR"] = Root,
            ["KONTEXT_UI_TEST"] = "1",
        },
    };

    public void Dispose()
    {
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
