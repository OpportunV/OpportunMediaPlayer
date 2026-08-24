using Avalonia;
using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMP.Lib;
using OMP.Lib.Session;
using OMP.Ui.Input;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Windows;
using Serilog;

namespace OMP.Ui;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Environment.SetEnvironmentVariable(
            "OMP_LOG_DIR",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        var bootstrapConfiguration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(bootstrapConfiguration)
            .CreateBootstrapLogger();

        try
        {
            var startupFilePath = args.Length > 0 ? args[0] : null;
            var singleInstanceCoordinator = SingleInstanceCoordinator.AcquireOrHandOff(startupFilePath);

            if (singleInstanceCoordinator.HandedOff)
            {
                Log.Information("Handed the open request off to the running instance.");
                return;
            }

            var appHost = Host.CreateDefaultBuilder(args)
                .UseContentRoot(AppContext.BaseDirectory)
                .UseSerilog((context, _, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration))
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(
                        context.Configuration.GetSection(PlaybackTuningOptions.SectionName).Get<PlaybackTuningOptions>()
                        ?? new PlaybackTuningOptions());
                    services.AddSingleton(new StartupOptions(startupFilePath));
                    services.AddSingleton(singleInstanceCoordinator);
                    services.AddSingleton(FFmpegLibraryLocator.CreateOptions());
                    services.AddSingleton<IMediaSessionRegistry, MediaSessionRegistry>();
                    services.AddTransient<IMainWindowCommands, MainWindowCommands>();
                    services.AddSingleton<IMainWindowHotkeyService, MainWindowHotkeyService>();
                    services.AddSingleton<IWindowFactory, WindowFactory>();
                    services.AddSingleton<IUserSettingsService, UserSettingsService>();
                    services.AddSingleton<IYtDlpResolver, YtDlpResolver>();

                    services.AddTransient<MainWindow>();
                    services.AddTransient<OptionsWindow>();
                    services.AddTransient<SubtitleZoneEditorWindow>();
                    services.AddTransient<HotkeysWindow>();
                    services.AddTransient<AboutWindow>();
                    services.AddTransient<AudioOutputWarningWindow>();
                    services.AddTransient<OpenFileErrorWindow>();
                    services.AddTransient<OpenUrlWindow>();
                })
                .Build();

            var services = appHost.Services;

            ApplyLanguageOverride(services.GetRequiredService<IUserSettingsService>().Current.Language);

            Log.Information("Application starting.");

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

    private static void ApplyLanguageOverride(string? languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo(languageCode);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
