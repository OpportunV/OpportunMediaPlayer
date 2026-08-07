using Avalonia;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMP.Lib.Session;
using OMP.Ui.Controls;
using OMP.Ui.DevTools;
using OMP.Ui.Input;

namespace OMP.Ui;

internal static class Program
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

                // Explicit factories, not services.AddTransient<MainWindow>(): the default
                // ServiceProvider activator only ever invokes a *public* constructor, and these
                // windows deliberately keep an internal one (nothing outside this assembly should
                // construct them directly).
                services.AddTransient(sp => new MainWindow(
                    sp.GetRequiredService<IMediaSessionRegistry>(),
                    sp.GetRequiredService<IMainWindowCommands>(),
                    sp.GetRequiredService<IMainWindowHotkeyService>(),
                    sp.GetRequiredService<IWindowFactory>()));
                services.AddTransient(sp => new OptionsWindow(sp.GetRequiredService<IMediaSessionRegistry>()));
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
