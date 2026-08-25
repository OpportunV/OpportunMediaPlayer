using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Moq;
using OMP.Ui.Controls;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Controls;

/// <summary>
/// Behaviour coverage for the General tab. Each control's event is now declared directly in
/// <c>OptionsGeneralTab.axaml</c>, so a dropped subscription fails the build rather than these
/// tests - what remains here is verifying each handler actually does the right thing.
/// </summary>
public class OptionsGeneralTabTests
{
    [AvaloniaFact]
    public void SelectorsAreSeededFromCurrentSettings()
    {
        var h = new Harness(theme: ThemeMode.Dark, language: null);

        Assert.Equal(ThemeMode.Dark, ((ThemeModeOption)h.Tab.ThemeSelector.SelectedItem!).Mode);
        Assert.Null(((LanguageOption)h.Tab.LanguageSelector.SelectedItem!).CultureCode);
    }

    [AvaloniaFact]
    public void ChangingTheme_PersistsIt()
    {
        var h = new Harness();

        h.Tab.ThemeSelector.SelectedItem = h.ThemeOptionFor(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, h.Settings.Theme);
        h.SettingsService.Verify(s => s.Save(), Times.AtLeastOnce);
    }

    [AvaloniaFact]
    public void ChangingLanguage_PersistsItAndOffersRestart()
    {
        var h = new Harness();
        Assert.False(h.Tab.RestartNowButton.IsVisible);

        h.Tab.LanguageSelector.SelectedItem = h.LanguageOptions.First(o => o.CultureCode is not null);

        Assert.NotNull(h.Settings.Language);
        Assert.True(h.Tab.RestartNowButton.IsVisible);
    }

    [AvaloniaFact]
    public void RestartButton_InvokesTheRestartCallback()
    {
        var h = new Harness();

        h.Tab.RestartNowButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, h.RestartCount);
    }

    [AvaloniaFact]
    public void YtDlpPath_IsSeededFromSettings()
    {
        var h = new Harness(ytDlpPath: @"C:\tools\yt-dlp.exe");

        Assert.Equal(@"C:\tools\yt-dlp.exe", h.Tab.YtDlpPathTextBox.Text);
    }

    [AvaloniaFact]
    public void LosingFocusOnTheYtDlpBox_PersistsTheTrimmedPath()
    {
        var h = new Harness();

        h.Tab.YtDlpPathTextBox.Text = "  /usr/bin/yt-dlp  ";
        h.Tab.YtDlpPathTextBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal("/usr/bin/yt-dlp", h.Settings.YtDlpPath);
    }

    [AvaloniaFact]
    public void BlankYtDlpPath_PersistsAsNullRatherThanEmpty()
    {
        var h = new Harness(ytDlpPath: "/usr/bin/yt-dlp");

        h.Tab.YtDlpPathTextBox.Text = "   ";
        h.Tab.YtDlpPathTextBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));

        Assert.Null(h.Settings.YtDlpPath);
    }

    [AvaloniaFact]
    public void ResetButton_ClearsThePathAndTheTextBox()
    {
        var h = new Harness(ytDlpPath: "/usr/bin/yt-dlp");

        h.Tab.ResetYtDlpPathButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(h.Settings.YtDlpPath);
        Assert.Equal(string.Empty, h.Tab.YtDlpPathTextBox.Text);
    }

    [AvaloniaFact]
    public void BrowseButton_PersistsThePathThePickerReturns()
    {
        var h = new Harness();
        h.FilePicker.Setup(p => p.PickFileAsync(It.IsAny<Window>(), It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync(@"C:\tools\yt-dlp.exe");

        h.RaiseBrowseClick();

        Assert.Equal(@"C:\tools\yt-dlp.exe", h.Settings.YtDlpPath);
        Assert.Equal(@"C:\tools\yt-dlp.exe", h.Tab.YtDlpPathTextBox.Text);
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
        public OptionsGeneralTab Tab { get; } = new();

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

            Tab.Initialize(new Window(), SettingsService.Object, FilePicker.Object, () => RestartCount++);
        }

        public void RaiseBrowseClick()
        {
            Tab.BrowseYtDlpPathButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public IEnumerable<LanguageOption> LanguageOptions => Tab.LanguageSelector.ItemsSource!.Cast<LanguageOption>();

        public ThemeModeOption ThemeOptionFor(ThemeMode mode) =>
            Tab.ThemeSelector.ItemsSource!.Cast<ThemeModeOption>().First(o => o.Mode == mode);
    }
}
