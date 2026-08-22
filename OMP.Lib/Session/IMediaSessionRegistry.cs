namespace OMP.Lib.Session;

public interface IMediaSessionRegistry
{
    public event Action<IMediaSessionRegistry> SessionChanged;

    public IMediaSession? Current { get; }

    public void Open(string filePath);

    public void Open(MediaOpenRequest request);

    public void Close();
}