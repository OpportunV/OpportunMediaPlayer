using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui;

public sealed partial class App : Application
{
    public IServiceProvider? Services { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = Services!.GetRequiredService<IUserSettingsService>().Current.Theme.ToThemeVariant();

        if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatableLifetime)
        {
            activatableLifetime.Activated += OnActivated;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services!.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        if (e is not FileActivatedEventArgs fileArgs)
        {
            return;
        }

        var path = fileArgs.Files.FirstOrDefault()?.TryGetLocalPath();

        if (path is null)
        {
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow mainWindow })
        {
            mainWindow.HandleExternalOpenRequest(path);
        }
        else
        {
            Services!.GetRequiredService<StartupOptions>().FilePath = path;
        }
    }
}
