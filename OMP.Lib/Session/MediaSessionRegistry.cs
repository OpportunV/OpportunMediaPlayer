namespace OMP.Lib.Session;

public sealed class MediaSessionRegistry : IMediaSessionRegistry
{
    public event Action<IMediaSessionRegistry>? SessionChanged;

    public IMediaSession? Current { get; private set; }

    private readonly PlaybackTuningOptions _options;

    public MediaSessionRegistry(PlaybackTuningOptions options)
    {
        _options = options;
    }

    public void Open(string filePath)
    {
        Current?.Dispose();
        Current = new MediaSession(filePath, _options);
        Current.SetSpeed(1);

        SessionChanged?.Invoke(this);
    }

    public void Close()
    {
        Current?.Dispose();
        Current = null;

        SessionChanged?.Invoke(this);
    }
}
