using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Moq;
using OMP.Lib;
using OMP.Ui.Services;
using OMP.Ui.Tests.TestDoubles;

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

    // Not covered: the failure path calls ShowError, which needs a real modal dialog. Closing that
    // gap needs a dialog seam - see the composition follow-up.

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

            Opener = new MediaOpener(
                new Window(),
                LoadingIndicator,
                EmptyStateLabel,
                Registry,
                new Mock<IYtDlpResolver>().Object,
                new Mock<IWindowFactory>().Object,
                new NativeLibraryOptions());

            Opener.MediaOpened += () => MediaOpenedCount++;
        }
    }
}
