using OMP.Lib.Session;

namespace OMP.Ui.Tests.TestDoubles;

internal sealed class FakeMediaSessionRegistry : IMediaSessionRegistry
{
    public IMediaSession? Current { get; set; }

    public string? LastOpenedFilePath { get; private set; }

    public int CloseCallCount { get; private set; }

    public event Action<IMediaSessionRegistry> SessionChanged = delegate { };

    public void Open(string filePath)
    {
        LastOpenedFilePath = filePath;
        SessionChanged?.Invoke(this);
    }

    public void Close()
    {
        CloseCallCount++;
        Current = null;
        SessionChanged?.Invoke(this);
    }
}
