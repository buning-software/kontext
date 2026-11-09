using System.Diagnostics;

namespace Kontext.UiTests.Application;

/// <summary>A bare Kontext.exe process, for the tests that care about the launch itself: arguments, exit codes and whether a window appears.</summary>
public sealed class KontextProcess : IDisposable
{
    private readonly Process _process;

    private KontextProcess(Process process) => _process = process;

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode;

    public bool HasWindow
    {
        get
        {
            _process.Refresh();
            return !_process.HasExited && _process.MainWindowHandle != IntPtr.Zero;
        }
    }

    public static KontextProcess Start(TestDataDirectory data, string arguments = "")
    {
        var info = new ProcessStartInfo(KontextApp.BuiltExecutable, arguments) { UseShellExecute = false };
        foreach (var (name, value) in data.Environment)
        {
            info.Environment[name] = value;
        }

        return new KontextProcess(Process.Start(info) ?? throw new InvalidOperationException("Kontext did not start."));
    }

    public bool WaitForExit(TimeSpan timeout) => _process.WaitForExit(timeout);

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(TimeSpan.FromSeconds(10));
        }

        _process.Dispose();
    }
}
