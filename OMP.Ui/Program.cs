using Avalonia;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMP.Lib.Session;

namespace OMP.Ui;

class Program
{
    public static IServiceProvider Services => _appHost.Services;

    private static IHost _appHost = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        _appHost = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IMediaSessionRegistry, MediaSessionRegistry>();

                services.AddTransient<MainWindow>();
                services.AddTransient<OptionsWindow>();
            })
            .Build();

        if (args.Length > 0)
        {
            var sessionRegistry = Services.GetRequiredService<IMediaSessionRegistry>();
            sessionRegistry.Open(args[0]);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}