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
using OMP.Ui.Settings;

namespace OMP.Ui.Services;

internal sealed class OptionsGeneralSection
{
    private static readonly FilePickerFileType _ytDlpFileTypeFilter = new(Strings.Options_YtDlpPathFileTypeFilterName)
    {
        Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]
    };

    private readonly Window _owner;
    private readonly ComboBox _themeSelector;
    private readonly ComboBox _languageSelector;
    private readonly Button _restartNowButton;
    private readonly TextBox _ytDlpPathTextBox;
    private readonly IUserSettingsService _settings;
    private readonly Action _restart;

    public OptionsGeneralSection(
        Window owner,
        ComboBox themeSelector,
        ComboBox languageSelector,
        Button restartNowButton,
        TextBox ytDlpPathTextBox,
        Button browseYtDlpPathButton,
        Button resetYtDlpPathButton,
        IUserSettingsService settings,
        Action restart)
    {
        _owner = owner;
        _themeSelector = themeSelector;
        _languageSelector = languageSelector;
        _restartNowButton = restartNowButton;
        _ytDlpPathTextBox = ytDlpPathTextBox;
        _settings = settings;
        _restart = restart;

        _themeSelector.ItemsSource = Enum.GetValues<ThemeMode>().Select(mode => new ThemeModeOption(mode)).ToList();
        _themeSelector.SelectedItem = _themeSelector.Items
            .Cast<ThemeModeOption>()
            .First(option => option.Mode == _settings.Current.Theme);

        var languageOptions = new List<LanguageOption> { new(null, Strings.Common_SystemDefault) };
        languageOptions.AddRange(AvailableLanguages.Cultures
            .OrderBy(culture => culture.NativeName)
            .Select(culture => new LanguageOption(culture.Name, culture.NativeName)));

        _languageSelector.ItemsSource = languageOptions;
        _languageSelector.SelectedItem = languageOptions
            .FirstOrDefault(option => option.CultureCode == _settings.Current.Language) ?? languageOptions[0];

        _ytDlpPathTextBox.Text = _settings.Current.YtDlpPath ?? string.Empty;

        _themeSelector.SelectionChanged += OnThemeChanged;
        _languageSelector.SelectionChanged += OnLanguageChanged;
        _restartNowButton.Click += OnRestartNowClick;
        _ytDlpPathTextBox.LostFocus += OnYtDlpPathChanged;
        browseYtDlpPathButton.Click += OnBrowseYtDlpPath;
        resetYtDlpPathButton.Click += OnResetYtDlpPath;
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_themeSelector.SelectedItem is not ThemeModeOption option)
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
        if (_languageSelector.SelectedItem is not LanguageOption option)
        {
            return;
        }

        if (option.CultureCode != _settings.Current.Language)
        {
            _restartNowButton.IsVisible = true;
        }

        _settings.Current.Language = option.CultureCode;
        _settings.Save();
    }

    private void OnYtDlpPathChanged(object? sender, RoutedEventArgs e) => SetYtDlpPath(_ytDlpPathTextBox.Text);

    private void OnResetYtDlpPath(object? sender, RoutedEventArgs e) => SetYtDlpPath(null);

    private async void OnBrowseYtDlpPath(object? sender, RoutedEventArgs e)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = Strings.Options_YtDlpPathBrowseTitle,
                AllowMultiple = false,
                FileTypeFilter = [_ytDlpFileTypeFilter]
            });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();

        if (path == null)
        {
            return;
        }

        SetYtDlpPath(path);
    }

    private void SetYtDlpPath(string? path)
    {
        var trimmed = path?.Trim();
        var normalized = string.IsNullOrEmpty(trimmed) ? null : trimmed;

        _ytDlpPathTextBox.Text = normalized ?? string.Empty;
        _settings.Current.YtDlpPath = normalized;
        _settings.Save();
    }

    private void OnRestartNowClick(object? sender, RoutedEventArgs e) => _restart();
}
