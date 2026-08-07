using Avalonia;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMP.Lib.Session;
using OMP.Ui.Controls;
using OMP.Ui.Debug;
using OMP.Ui.Input;

namespace OMP.Ui;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var appHost = Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                services.Configure<DebugOptions>(context.Configuration.GetSection(DebugOptions.SectionName));

                services.AddSingleton<IMediaSessionRegistry, MediaSessionRegistry>();
                services.AddTransient<IMainWindowCommands, MainWindowCommands>();
                services.AddSingleton<IMainWindowHotkeyService, MainWindowHotkeyService>();
                services.AddSingleton<IWindowFactory, WindowFactory>();
                services.AddSingleton<DebugTrackAutoRouter>();

                services.AddTransient<MainWindow>();
                services.AddTransient<OptionsWindow>();
            })
            .Build();

        var services = appHost.Services;

        // Resolved once, purely for its constructor side effect of subscribing to session changes.
        services.GetRequiredService<DebugTrackAutoRouter>();

        if (args.Length > 0)
        {
            services.GetRequiredService<IMediaSessionRegistry>().Open(args[0]);
        }

        BuildAvaloniaApp()
            .AfterSetup(builder => ((App)builder.Instance!).Services = services)
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
