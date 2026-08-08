using Avalonia;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMP.Lib;
using OMP.Lib.Session;
using OMP.Ui.Controls;
using OMP.Ui.Input;
using OMP.Ui.Settings;
using Serilog;

namespace OMP.Ui;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var appHost = Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory)
                .UseSerilog((context, _, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration))
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(
                        context.Configuration.GetSection(PlaybackTuningOptions.SectionName).Get<PlaybackTuningOptions>()
                        ?? new PlaybackTuningOptions());
                    services.AddSingleton<IMediaSessionRegistry, MediaSessionRegistry>();
                    services.AddTransient<IMainWindowCommands, MainWindowCommands>();
                    services.AddSingleton<IMainWindowHotkeyService, MainWindowHotkeyService>();
                    services.AddSingleton<IWindowFactory, WindowFactory>();
                    services.AddSingleton<IUserSettingsService, UserSettingsService>();

                    services.AddTransient<MainWindow>();
                    services.AddTransient<OptionsWindow>();
                    services.AddTransient<SubtitleZoneEditorWindow>();
                })
                .Build();

            var services = appHost.Services;

            Log.Information("Application starting.");

            if (args.Length > 0)
            {
                services.GetRequiredService<IMediaSessionRegistry>().Open(args[0]);
            }

            BuildAvaloniaApp()
                .AfterSetup(builder => ((App)builder.Instance!).Services = services)
                .StartWithClassicDesktopLifetime(args);

            Log.Information("Application stopping.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
