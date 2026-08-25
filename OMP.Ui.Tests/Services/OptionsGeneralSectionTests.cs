using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Moq;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Services;

/// <summary>
/// Wiring coverage for the General tab. Every control here was previously bound through a XAML
/// attribute and is now bound with <c>+=</c>, which fails silently if dropped.
/// </summary>
public class OptionsGeneralSectionTests
{
    [AvaloniaFact]
    public void SelectorsAreSeededFromCurrentSettings()
    {
        var h = new Harness(theme: ThemeMode.Dark, language: null);

        Assert.Equal(ThemeMode.Dark, ((ThemeModeOption)h.ThemeSelector.SelectedItem!).Mode);
        Assert.Null(((LanguageOption)h.LanguageSelector.SelectedItem!).CultureCode);
    }

    [AvaloniaFact]
    public void ChangingTheme_PersistsIt()
    {
        var h = new Harness();

        h.ThemeSelector.SelectedItem = h.ThemeOptionFor(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, h.Settings.Theme);
        h.SettingsService.Verify(s => s.Save(), Times.AtLeastOnce);
    }

    [AvaloniaFact]
    public void ChangingLanguage_PersistsItAndOffersRestart()
    {
        var h = new Harness();
        Assert.False(h.RestartNowButton.IsVisible);

        h.LanguageSelector.SelectedItem = h.LanguageOptions.First(o => o.CultureCode is not null);

        Assert.NotNull(h.Settings.Language);
        Assert.True(h.RestartNowButton.IsVisible);
    }

    [AvaloniaFact]
    public void RestartButton_InvokesTheRestartCallback()
    {
        var h = new Harness();

        h.RestartNowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, h.RestartCount);
    }

    [AvaloniaFact]
    public void YtDlpPath_IsSeededFromSettings()
    {
        var h = new Harness(ytDlpPath: @"C:\tools\yt-dlp.exe");

        Assert.Equal(@"C:\tools\yt-dlp.exe", h.YtDlpPathTextBox.Text);
    }

    [AvaloniaFact]
    public void LosingFocusOnTheYtDlpBox_PersistsTheTrimmedPath()
    {
        var h = new Harness();

        h.YtDlpPathTextBox.Text = "  /usr/bin/yt-dlp  ";
        h.YtDlpPathTextBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal("/usr/bin/yt-dlp", h.Settings.YtDlpPath);
    }

    [AvaloniaFact]
    public void BlankYtDlpPath_PersistsAsNullRatherThanEmpty()
    {
        var h = new Harness(ytDlpPath: "/usr/bin/yt-dlp");

        h.YtDlpPathTextBox.Text = "   ";
        h.YtDlpPathTextBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));

        Assert.Null(h.Settings.YtDlpPath);
    }

    [AvaloniaFact]
    public void ResetButton_ClearsThePathAndTheTextBox()
    {
        var h = new Harness(ytDlpPath: "/usr/bin/yt-dlp");

        h.ResetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(h.Settings.YtDlpPath);
        Assert.Equal(string.Empty, h.YtDlpPathTextBox.Text);
    }

    [AvaloniaFact]
    public void BrowseButton_PersistsThePathThePickerReturns()
    {
        var h = new Harness();
        h.FilePicker.Setup(p => p.PickFileAsync(It.IsAny<Window>(), It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync(@"C:\tools\yt-dlp.exe");

        h.RaiseBrowseClick();

        Assert.Equal(@"C:\tools\yt-dlp.exe", h.Settings.YtDlpPath);
        Assert.Equal(@"C:\tools\yt-dlp.exe", h.YtDlpPathTextBox.Text);
    }

    [AvaloniaFact]
    public void BrowseButton_CancelledPicker_LeavesThePathUnchanged()
    {
        var h = new Harness(ytDlpPath: "/usr/bin/yt-dlp");
        h.FilePicker.Setup(p => p.PickFileAsync(It.IsAny<Window>(), It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync((string?)null);

        h.RaiseBrowseClick();

        Assert.Equal("/usr/bin/yt-dlp", h.Settings.YtDlpPath);
    }

    private sealed class Harness
    {
        public ComboBox ThemeSelector { get; } = new();

        public ComboBox LanguageSelector { get; } = new();

        public Button RestartNowButton { get; } = new() { IsVisible = false };

        public TextBox YtDlpPathTextBox { get; } = new();

        public Button BrowseButton { get; } = new();

        public Button ResetButton { get; } = new();

        public UserSettings Settings { get; } = new();

        public Mock<IUserSettingsService> SettingsService { get; } = new();

        public Mock<IFilePickerService> FilePicker { get; } = new();

        public int RestartCount { get; private set; }

        public Harness(ThemeMode theme = ThemeMode.System, string? language = null, string? ytDlpPath = null)
        {
            Settings.Theme = theme;
            Settings.Language = language;
            Settings.YtDlpPath = ytDlpPath;
            SettingsService.Setup(s => s.Current).Returns(Settings);

            _ = new OptionsGeneralSection(
                new Window(),
                ThemeSelector,
                LanguageSelector,
                RestartNowButton,
                YtDlpPathTextBox,
                BrowseButton,
                ResetButton,
                SettingsService.Object,
                FilePicker.Object,
                () => RestartCount++);
        }

        public void RaiseBrowseClick()
        {
            BrowseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public IEnumerable<LanguageOption> LanguageOptions => LanguageSelector.ItemsSource!.Cast<LanguageOption>();

        public ThemeModeOption ThemeOptionFor(ThemeMode mode) =>
            ThemeSelector.ItemsSource!.Cast<ThemeModeOption>().First(o => o.Mode == mode);
    }
}
