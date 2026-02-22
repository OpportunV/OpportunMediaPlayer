namespace OMP.Lib.Session;

public sealed class MediaSessionRegistry : IMediaSessionRegistry
{
    public event Action<IMediaSessionRegistry>? SessionChanged;

    public IMediaSession? Current { get; private set; }

    public void Open(string filePath)
    {
        Current?.Dispose();
        Current = new MediaSession(filePath);

        SessionChanged?.Invoke(this);
    }

    public void Close()
    {
        Current?.Dispose();
        Current = null;

        SessionChanged?.Invoke(this);
    }
}