using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OMP.Ui.Extensions;
using OMP.Ui.Localization;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Controls;

internal sealed partial class OptionsGeneralTab : UserControl
{
    private static readonly FilePickerFileType _ytDlpFileTypeFilter = new(Strings.Options_YtDlpPathFileTypeFilterName)
    {
        Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]
    };

    private Window _owner = null!;
    private IUserSettingsService _settings = null!;
    private IFilePickerService _filePicker = null!;
    private Action _restart = null!;

    public OptionsGeneralTab()
    {
        InitializeComponent();
    }

    public void Initialize(Window owner, IUserSettingsService settings, IFilePickerService filePicker, Action restart)
    {
        _owner = owner;
        _settings = settings;
        _filePicker = filePicker;
        _restart = restart;

        ThemeSelector.ItemsSource = Enum.GetValues<ThemeMode>().Select(mode => new ThemeModeOption(mode)).ToList();
        ThemeSelector.SelectedItem = ThemeSelector.Items
            .Cast<ThemeModeOption>()
            .First(option => option.Mode == _settings.Current.Theme);

        var languageOptions = new List<LanguageOption> { new(null, Strings.Common_SystemDefault) };
        languageOptions.AddRange(AvailableLanguages.Cultures
            .OrderBy(culture => culture.NativeName)
            .Select(culture => new LanguageOption(culture.Name, culture.NativeName)));

        LanguageSelector.ItemsSource = languageOptions;
        LanguageSelector.SelectedItem = languageOptions
            .FirstOrDefault(option => option.CultureCode == _settings.Current.Language) ?? languageOptions[0];

        YtDlpPathTextBox.Text = _settings.Current.YtDlpPath ?? string.Empty;
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is not ThemeModeOption option)
        {
            return;
        }

        _settings.Current.Theme = option.Mode;
        _settings.Save();

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = option.Mode.ToThemeVariant();
        }
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageSelector.SelectedItem is not LanguageOption option)
        {
            return;
        }

        if (option.CultureCode != _settings.Current.Language)
        {
            RestartNowButton.IsVisible = true;
        }

        _settings.Current.Language = option.CultureCode;
        _settings.Save();
    }

    private void OnYtDlpPathChanged(object? sender, RoutedEventArgs e) => SetYtDlpPath(YtDlpPathTextBox.Text);

    private void OnResetYtDlpPath(object? sender, RoutedEventArgs e) => SetYtDlpPath(null);

    private async void OnBrowseYtDlpPath(object? sender, RoutedEventArgs e)
    {
        var path = await _filePicker.PickFileAsync(_owner, Strings.Options_YtDlpPathBrowseTitle, _ytDlpFileTypeFilter);

        if (path is not null)
        {
            SetYtDlpPath(path);
        }
    }

    private void SetYtDlpPath(string? path)
    {
        var trimmed = path?.Trim();
        var normalized = string.IsNullOrEmpty(trimmed) ? null : trimmed;

        YtDlpPathTextBox.Text = normalized ?? string.Empty;
        _settings.Current.YtDlpPath = normalized;
        _settings.Save();
    }

    private void OnRestartNowClick(object? sender, RoutedEventArgs e) => _restart();
}
