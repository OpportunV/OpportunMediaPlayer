using OMP.Lib.Session;

namespace OMP.Ui.Tests.TestDoubles;

internal sealed class FakeMediaSessionRegistry : IMediaSessionRegistry
{
    public IMediaSession? Current { get; set; }

    public string? LastOpenedFilePath { get; private set; }

    public int CloseCallCount { get; private set; }

    public Exception? OpenShouldThrow { get; set; }

    public event Action<IMediaSessionRegistry> SessionChanged = delegate { };

    public void Open(string filePath)
    {
        ThrowIfConfigured();
        LastOpenedFilePath = filePath;
        SessionChanged?.Invoke(this);
    }

    public void Open(MediaOpenRequest request)
    {
        ThrowIfConfigured();
        LastOpenedFilePath = request.PrimarySource;
        SessionChanged?.Invoke(this);
    }

    private void ThrowIfConfigured()
    {
        if (OpenShouldThrow is not null)
        {
            throw OpenShouldThrow;
        }
    }

    public void Close()
    {
        CloseCallCount++;
        Current = null;
        SessionChanged?.Invoke(this);
    }
}
