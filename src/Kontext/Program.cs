using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Kontext.Startup;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace Kontext;

public static class Program
{
    private const string InstanceKey = "Kontext.Main";
    private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(5);
    private static readonly object Gate = new();
    private static Action? s_secondInstanceHandler;
    private static bool s_secondInstancePending;

    internal static bool StartedAtSignIn { get; private set; }

    internal static void HandleSecondInstance(Action handler)
    {
        lock (Gate)
        {
            s_secondInstanceHandler = handler;
            if (!s_secondInstancePending)
            {
                return;
            }

            s_secondInstancePending = false;
        }

        handler();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var current = AppInstance.GetCurrent();
        StartedAtSignIn = StartupActivation.HasFlag(args) || IsSignInLaunch(current.GetActivatedEventArgs());

        if (!BecomeTheMainInstance(current))
        {
            return 0;
        }

        Application.Start(callback =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });

        return 0;
    }

    private static bool BecomeTheMainInstance(AppInstance current)
    {
        var flavour = PackageIdentity.IsPackaged ? string.Empty : ".Unpackaged";
        var key = InstanceKey + flavour + DataDirectorySuffix();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var main = AppInstance.FindOrRegisterForKey(key);
            if (main.IsCurrent)
            {
                main.Activated += (_, activation) => OnSecondInstance(activation);
                return true;
            }

            if (!StartedAtSignIn)
            {
                AllowSetForegroundWindow((uint)main.ProcessId);
            }

            if (TryRedirect(main, current.GetActivatedEventArgs()))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryRedirect(AppInstance main, AppActivationArguments activation)
    {
        try
        {
            return Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait(RedirectTimeout);
        }
        catch (AggregateException)
        {
            return false;
        }
    }

    private static void OnSecondInstance(AppActivationArguments activation)
    {
        if (IsSignInLaunch(activation))
        {
            return;
        }

        Action? handler;
        lock (Gate)
        {
            handler = s_secondInstanceHandler;
            if (handler is null)
            {
                s_secondInstancePending = true;
                return;
            }
        }

        handler();
    }

    private static bool IsSignInLaunch(AppActivationArguments activation) =>
        activation.Kind == ExtendedActivationKind.StartupTask
        || (activation.Kind == ExtendedActivationKind.Launch
            && activation.Data is ILaunchActivatedEventArgs launch
            && launch.Arguments.Contains(StartupActivation.Flag, StringComparison.OrdinalIgnoreCase));

    private static string DataDirectorySuffix()
    {
        var overridden = Environment.GetEnvironmentVariable("KONTEXT_DATA_DIR");
        return string.IsNullOrWhiteSpace(overridden)
            ? string.Empty
            : "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(overridden.ToUpperInvariant())), 0, 8);
    }

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);
#pragma warning restore SYSLIB1054
}
