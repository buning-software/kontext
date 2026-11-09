using Kontext.Claude;
using Kontext.Tray;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Kontext;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        InitializeComponent();

        _host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration(configuration => configuration.AddJsonFile("appsettings.local.json", optional: true))
            .ConfigureServices((context, services) => services.AddKontext(context.Configuration))
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host.Services.GetRequiredService<ClaudeEnvironmentScrubber>().Scrub();

        var window = _host.Services.GetRequiredService<MainWindow>();

        window.Closed += (_, _) => _host.Dispose();

        var tray = _host.Services.GetRequiredService<TrayHost>();
        tray.Start();

        if (Program.StartedAtSignIn)
        {
            window.AppWindow.Show(activateWindow: false);
            window.AppWindow.Hide();
        }
        else
        {
            window.Activate();
        }

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        Program.HandleSecondInstance(() => dispatcher.TryEnqueue(tray.Restore));
    }
}
