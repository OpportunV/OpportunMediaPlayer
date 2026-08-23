using Microsoft.Extensions.Logging;

namespace OMP.Lib.Session;

public sealed class MediaSessionRegistry(
    PlaybackTuningOptions options,
    ILoggerFactory loggerFactory,
    NativeLibraryOptions nativeLibraryOptions)
    : IMediaSessionRegistry
{
    public event Action<IMediaSessionRegistry>? SessionChanged;

    public IMediaSession? Current { get; private set; }

    private readonly ILogger _logger = loggerFactory.CreateLogger<MediaSessionRegistry>();

    public void Open(string filePath) => Open(MediaOpenRequest.ForFile(filePath));

    public void Open(MediaOpenRequest request)
    {
        _logger.LogInformation(
            "Opening {PrimarySource} with {SidecarCount} audio sidecar(s).",
            request.PrimarySource,
            request.AudioSidecars.Count);

        Current?.Dispose();

        try
        {
            Current = new MediaSession(request, options, loggerFactory, nativeLibraryOptions);
        }
        catch (Exception ex)
        {
            Current = null;
            _logger.LogError(ex, "Failed to open {PrimarySource}.", request.PrimarySource);
            throw;
        }

        Current.SetSpeed(1);

        SessionChanged?.Invoke(this);
    }

    public void Close()
    {
        _logger.LogInformation("Closing session.");

        Current?.Dispose();
        Current = null;

        SessionChanged?.Invoke(this);
    }
}
