using System;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using OMP.Lib.Session;
using OMP.Ui.Helpers;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Windows;

public sealed partial class OptionsWindow : Window
{
    public OptionsWindow(IMediaSessionRegistry mediaSessionRegistry, IUserSettingsService settings,
        IWindowFactory windowFactory, IFilePickerService filePicker, ILoggerFactory loggerFactory,
        SingleInstanceCoordinator singleInstanceCoordinator)
    {
        InitializeComponent();

        GeneralTab.Initialize(
            this,
            settings,
            filePicker,
            () => ApplicationRestart.Restart(
                mediaSessionRegistry.Current?.FilePath, singleInstanceCoordinator));

        AudioRoutingTab.Initialize(mediaSessionRegistry, settings, loggerFactory);

        ZonesTab.Initialize(this, windowFactory, settings);

        SubtitleRoutingTab.Initialize(this, ZonesTab, mediaSessionRegistry, windowFactory, filePicker, loggerFactory);
    }

    protected override void OnClosed(EventArgs e)
    {
        SubtitleRoutingTab.Dispose();

        base.OnClosed(e);
    }
}
