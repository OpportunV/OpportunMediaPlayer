using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using Moq;
using OMP.Lib;
using OMP.Ui.Services;
using OMP.Ui.Tests.TestDoubles;
using OMP.Ui.Windows;

namespace OMP.Ui.Tests.Services;

/// <summary>
/// Opening a file always routes through <see cref="MediaOpener.OpenPathAsync"/>, whichever entry
/// point started it, so these cover the shared behaviour: the loading indicator only appears for
/// network sources, the resolved title does not survive into the next open, and callers are told
/// when a session actually opened.
/// </summary>
public class MediaOpenerTests
{
    [AvaloniaFact]
    public async Task OpenPathAsync_OpensTheFileAndAnnouncesIt()
    {
        var h = new Harness();

        await h.Opener.OpenPathAsync(@"C:\media\clip.mp4");

        Assert.Equal(@"C:\media\clip.mp4", h.Registry.LastOpenedFilePath);
        Assert.Equal(1, h.MediaOpenedCount);
    }

    [AvaloniaFact]
    public async Task OpenPathAsync_LocalFile_NeverShowsTheLoadingIndicator()
    {
        var h = new Harness();

        await h.Opener.OpenPathAsync(@"C:\media\clip.mp4");

        Assert.False(h.LoadingIndicator.IsVisible);
        Assert.False(h.LoadingWasEverVisible);
    }

    [AvaloniaFact]
    public async Task OpenPathAsync_LeavesNoResolvedTitleForALocalFile()
    {
        var h = new Harness();

        await h.Opener.OpenPathAsync(@"C:\media\clip.mp4");

        // The window falls back to the file name when this is null, so a title left over from a
        // previously opened stream would silently mislabel the next local file.
        Assert.Null(h.Opener.ResolvedTitle);
    }

    [AvaloniaFact]
    public async Task OpenPathAsync_SessionOpenFails_ShowsTheErrorDialogAndDoesNotAnnounceAnOpen()
    {
        var h = new Harness();
        h.Registry.OpenShouldThrow = new InvalidOperationException("codec not found");

        await h.Opener.OpenPathAsync(@"C:\media\bad.mp4");

        Assert.Equal(0, h.MediaOpenedCount);
        h.WindowFactory.Verify(
            f => f.ShowDialogAsync(h.Owner, It.IsAny<Action<OpenFileErrorWindow>>()),
            Times.Once);
    }

    [AvaloniaFact]
    public async Task OpenFileAsync_OpensWhateverThePickerReturns()
    {
        var h = new Harness();
        h.FilePicker.Setup(p => p.PickFileAsync(h.Owner, It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync(@"C:\media\picked.mp4");

        await h.Opener.OpenFileAsync();

        Assert.Equal(@"C:\media\picked.mp4", h.Registry.LastOpenedFilePath);
    }

    [AvaloniaFact]
    public async Task OpenFileAsync_CancelledPicker_OpensNothing()
    {
        var h = new Harness();
        h.FilePicker.Setup(p => p.PickFileAsync(h.Owner, It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync((string?)null);

        await h.Opener.OpenFileAsync();

        Assert.Null(h.Registry.LastOpenedFilePath);
    }

    [AvaloniaFact]
    public void Construction_OnLinux_ReplacesTheEmptyStateLabelInsteadOfEnablingDrop()
    {
        var h = new Harness();

        // Drag and drop is unsupported on Linux, so the hint has to differ there and nowhere else.
        var expectSameAsDefault = !OperatingSystem.IsLinux();
        Assert.Equal(expectSameAsDefault, h.EmptyStateLabel.Text == Harness.DefaultEmptyStateText);
    }

    private sealed class Harness
    {
        public const string DefaultEmptyStateText = "drop-a-file";

        public Control LoadingIndicator { get; } = new Border { IsVisible = false };

        public TextBlock EmptyStateLabel { get; } = new() { Text = DefaultEmptyStateText };

        public FakeMediaSessionRegistry Registry { get; } = new();

        public Mock<IWindowFactory> WindowFactory { get; } = new();

        public Mock<IFilePickerService> FilePicker { get; } = new();

        public Window Owner { get; } = new();

        public MediaOpener Opener { get; }

        public int MediaOpenedCount { get; private set; }

        public bool LoadingWasEverVisible { get; private set; }

        public Harness()
        {
            LoadingIndicator.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty && LoadingIndicator.IsVisible)
                {
                    LoadingWasEverVisible = true;
                }
            };

            WindowFactory
                .Setup(f => f.ShowDialogAsync(It.IsAny<Window>(), It.IsAny<Action<OpenFileErrorWindow>>()))
                .Returns(Task.CompletedTask);

            Opener = new MediaOpener(
                Owner,
                LoadingIndicator,
                EmptyStateLabel,
                Registry,
                new Mock<IYtDlpResolver>().Object,
                WindowFactory.Object,
                FilePicker.Object,
                new NativeLibraryOptions());

            Opener.MediaOpened += () => MediaOpenedCount++;
        }
    }
}
