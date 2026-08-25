using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using OMP.Lib;
using OMP.Lib.Session;
using OMP.Ui.Helpers;
using OMP.Ui.Localization;
using OMP.Ui.Windows;

namespace OMP.Ui.Services;

/// <summary>
/// Every way a file or stream gets opened: the file picker, the Open URL dialog and its retry
/// loop, drag and drop, and the paths a startup argument or a second instance come in through.
/// Owns the loading indicator and the error dialog that go with them.
/// </summary>
internal sealed class MediaOpener
{
    private static readonly FilePickerFileType _mediaFileTypeFilter = new(Strings.MainWindow_OpenFileTypeFilterName)
    {
        Patterns =
        [
            "*.mp4", "*.mkv", "*.avi", "*.webm", "*.mov", "*.flv", "*.wmv",
            "*.mp3", "*.flac", "*.wav", "*.ogg", "*.m4a", "*.aac"
        ]
    };

    public event Action? MediaOpened;

    public string? ResolvedTitle { get; private set; }

    private readonly Window _owner;
    private readonly Control _loadingIndicator;
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IYtDlpResolver _ytDlpResolver;
    private readonly IWindowFactory _windowFactory;
    private readonly IFilePickerService _filePicker;
    private readonly NativeLibraryOptions _nativeLibraryOptions;

    private bool _isResolvingUrl;

    public MediaOpener(
        Window owner,
        Control loadingIndicator,
        TextBlock emptyStateLabel,
        IMediaSessionRegistry mediaSessionRegistry,
        IYtDlpResolver ytDlpResolver,
        IWindowFactory windowFactory,
        IFilePickerService filePicker,
        NativeLibraryOptions nativeLibraryOptions)
    {
        _owner = owner;
        _loadingIndicator = loadingIndicator;
        _mediaSessionRegistry = mediaSessionRegistry;
        _ytDlpResolver = ytDlpResolver;
        _windowFactory = windowFactory;
        _filePicker = filePicker;
        _nativeLibraryOptions = nativeLibraryOptions;

        if (OperatingSystem.IsLinux())
        {
            emptyStateLabel.Text = Strings.MainWindow_EmptyStateLabelNoDragDrop;
            return;
        }

        DragDrop.SetAllowDrop(_owner, true);
        _owner.AddHandler(DragDrop.DropEvent, OnFileDrop);
        _owner.AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
    }

    public async Task OpenFileAsync()
    {
        var path = await _filePicker.PickFileAsync(_owner, Strings.MainWindow_OpenFileDialogTitle, _mediaFileTypeFilter);

        if (path is not null)
        {
            await OpenPathAsync(path);
        }
    }

    public async Task OpenPathAsync(string path)
    {
        ResolvedTitle = null;

        if (await TryOpenSessionAsync(MediaOpenRequest.ForFile(path)))
        {
            MediaOpened?.Invoke();
        }
    }

    /// <summary>
    /// Loops rather than returning on failure: a resolved stream URL can go stale between the
    /// dialog and the open, so one re-resolve is attempted before the dialog is shown again with
    /// the page URL prefilled.
    /// </summary>
    public async Task OpenUrlAsync()
    {
        if (_isResolvingUrl)
        {
            return;
        }

        _isResolvingUrl = true;

        try
        {
            string? prefillUrl = null;

            while (true)
            {
                var result = await _windowFactory.ShowDialogAsync<OpenUrlWindow, YtDlpResolveResult>(
                    _owner, w => w.Load(prefillUrl));

                if (result is null)
                {
                    return;
                }

                ResolvedTitle = result.Title;
                var request = new MediaOpenRequest(result.Url!, result.AudioSidecars, result.Headers, result.SubtitleSidecars);

                if (await OpenSessionAsync(request) is null)
                {
                    MediaOpened?.Invoke();
                    return;
                }

                var retryResult = await _ytDlpResolver.ResolveAsync(result.PageUrl, CancellationToken.None);
                var retryRequest = request;

                if (retryResult.Status == YtDlpResolveStatus.Success)
                {
                    ResolvedTitle = retryResult.Title;
                    retryRequest = new MediaOpenRequest(
                        retryResult.Url!, retryResult.AudioSidecars, retryResult.Headers, retryResult.SubtitleSidecars);
                }

                if (await TryOpenSessionAsync(retryRequest))
                {
                    MediaOpened?.Invoke();
                    return;
                }

                prefillUrl = result.PageUrl;
            }
        }
        finally
        {
            _isResolvingUrl = false;
        }
    }

    private async Task<bool> TryOpenSessionAsync(MediaOpenRequest request)
    {
        var error = await OpenSessionAsync(request);
        if (error is null)
        {
            return true;
        }

        var heading = OperatingSystem.IsMacOS() && _nativeLibraryOptions.FFmpegLibraryDirectory is null
            ? Strings.OpenFileError_FFmpegMacHeading
            : Strings.OpenFileError_Heading;

        await ShowError(heading, error.Message);
        return false;
    }

    private async Task<Exception?> OpenSessionAsync(MediaOpenRequest request)
    {
        var isNetworkSource = UrlType.IsHttpUrl(request.PrimarySource);
        if (isNetworkSource)
        {
            _loadingIndicator.IsVisible = true;
        }

        try
        {
            await Task.Run(() => _mediaSessionRegistry.Open(request));
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
        finally
        {
            if (isNetworkSource)
            {
                _loadingIndicator.IsVisible = false;
            }
        }
    }

    private async Task ShowError(string heading, string reason) =>
        await _windowFactory.ShowDialogAsync<OpenFileErrorWindow>(_owner, w => w.Load(heading, reason));

    private static void OnFileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();

        if (path == null || !MediaFileType.IsSupportedMediaFile(path, _mediaFileTypeFilter.Patterns!))
        {
            return;
        }

        _owner.Activate();
        await OpenPathAsync(path);
    }
}
