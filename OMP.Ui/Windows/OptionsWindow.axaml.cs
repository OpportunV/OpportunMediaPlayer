using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using OMP.Lib.Session;
using OMP.Ui.Helpers;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Windows;

public sealed partial class OptionsWindow : Window
{
    private readonly OptionsSubtitleZonesSection _zonesSection;
    private readonly OptionsSubtitleRoutingSection _subtitleRoutingSection;
    private readonly OptionsAudioRoutingSection _audioRoutingSection;

    public OptionsWindow(IMediaSessionRegistry mediaSessionRegistry, IUserSettingsService settings,
        IWindowFactory windowFactory, IFilePickerService filePicker, ILoggerFactory loggerFactory,
        SingleInstanceCoordinator singleInstanceCoordinator)
    {
        InitializeComponent();

        _zonesSection = new OptionsSubtitleZonesSection(this, ZonesList, AddZoneButton, windowFactory, settings);

        _ = new OptionsGeneralSection(
            this,
            ThemeSelector,
            LanguageSelector,
            RestartNowButton,
            YtDlpPathTextBox,
            BrowseYtDlpPathButton,
            ResetYtDlpPathButton,
            settings,
            filePicker,
            () => ApplicationRestart.Restart(
                mediaSessionRegistry.Current?.FilePath, singleInstanceCoordinator));

        _subtitleRoutingSection = new OptionsSubtitleRoutingSection(
            this,
            SubtitleRoutesList,
            SubtitleStreamSelector,
            SubtitleZoneSelector,
            ClearDraftSubtitleRouteButton,
            LoadSubtitleFileButton,
            SubtitleRouteErrorText,
            _zonesSection,
            mediaSessionRegistry,
            windowFactory,
            filePicker,
            loggerFactory);

        _audioRoutingSection = new OptionsAudioRoutingSection(
            RoutesList,
            OutputSelector,
            StreamSelector,
            ClearDraftRouteButton,
            mediaSessionRegistry,
            settings,
            loggerFactory);
    }

    protected override void OnClosed(EventArgs e)
    {
        _subtitleRoutingSection.Dispose();

        base.OnClosed(e);
    }

    private void OnDeleteRoute(object? sender, RoutedEventArgs e) =>
        _audioRoutingSection.OnDeleteRoute(sender, e);

    private void OnRouteStreamChanged(object? sender, SelectionChangedEventArgs e) =>
        _audioRoutingSection.OnRouteStreamChanged(sender, e);

    private void OnRouteVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        _audioRoutingSection.OnRouteVolumeChanged(sender, e);

    private void OnRouteVolumeReleased(object? sender, PointerCaptureLostEventArgs e) =>
        _audioRoutingSection.OnRouteVolumeReleased(sender, e);

    private void OnRouteMuteChanged(object? sender, RoutedEventArgs e) =>
        _audioRoutingSection.OnRouteMuteChanged(sender, e);

    private void OnRouteDelayChanged(object? sender, NumericUpDownValueChangedEventArgs e) =>
        _audioRoutingSection.OnRouteDelayChanged(sender, e);

    private void OnEditZone(object? sender, RoutedEventArgs e) => _zonesSection.OnEditZone(sender, e);

    private void OnResetZone(object? sender, RoutedEventArgs e) => _zonesSection.OnResetZone(sender, e);

    private void OnDeleteZone(object? sender, RoutedEventArgs e) => _zonesSection.OnDeleteZone(sender, e);

    private void OnDeleteSubtitleRoute(object? sender, RoutedEventArgs e) =>
        _subtitleRoutingSection.OnDeleteSubtitleRoute(sender, e);
}
