using System.Reflection;
using NUnit.Framework.Interfaces;
using WinUia;
using WinUia.Models;
using WinUia.Recording;

namespace Kontext.UiTests.Application;

/// <summary>The running Kontext process. Its window is described by page objects over the elements they hold.</summary>
public sealed class KontextApp : App
{
    private const string ExePathVariable = "KONTEXT_EXE";
    private const string ExeName = "Kontext.exe";

    /// <summary>The Kontext.exe the tests drive: the KONTEXT_EXE variable, else the build next to the tests.</summary>
    public static string BuiltExecutable => Environment.GetEnvironmentVariable(ExePathVariable) is { Length: > 0 } overridden
        ? overridden
        : FindBuiltExecutable();

    public SidebarPage Sidebar => new(MainWindow);

    public SettingsPage Settings => new(MainWindow);

    public IssueDetailPage IssueDetail => new(MainWindow);

    public GitTabPage GitTab => new(MainWindow);

    protected override string ExecutablePath => BuiltExecutable;

    protected override AppLaunchOptions DefaultOptions => new()
    {
        ShowPointer = true,
        Recording = new RecordingOptions
        {
            FileName = () => TestContext.CurrentContext.Test.FullName,
            Failed = () => TestContext.CurrentContext.Result.Outcome.Status is TestStatus.Failed or TestStatus.Inconclusive,
        },
    };

    public static KontextApp Launch(TestDataDirectory data) => Launch<KontextApp>(data.LaunchOptions);

    private static string FindBuiltExecutable()
    {
        var binDirectory = typeof(KontextApp).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "KontextBinDirectory").Value ?? string.Empty;

        var executables = Directory.Exists(binDirectory)
            ? Directory.EnumerateFiles(binDirectory, ExeName, SearchOption.AllDirectories).ToArray()
            : [];

        return executables.Length == 1
            ? executables[0]
            : throw new FileNotFoundException(
                $"Expected exactly one {ExeName} under '{binDirectory}' but found {executables.Length}. Build src/Kontext in the same configuration, or set {ExePathVariable}.");
    }
}
