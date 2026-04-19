namespace OMP.Lib.Session;

public sealed class MediaSessionRegistry : IMediaSessionRegistry
{
    public event Action<IMediaSessionRegistry>? SessionChanged;

    public IMediaSession? Current { get; private set; }

    public void Open(string filePath)
    {
        Current?.Dispose();
        Current = new MediaSession(filePath);
        Current.SetSpeed(1);
        
        // For local testing.
        AddAllTracksForTests();
    }

    public void Close()
    {
        Current?.Dispose();
        Current = null;

        SessionChanged?.Invoke(this);
    }

    private void AddAllTracksForTests()
    {
        var routes = Current!.AudioStreams
            .Zip(Current.AudioOutputs)
            .ToList();
            
        Console.WriteLine($"Audio streams count: {routes.Count}");
        Console.WriteLine(
            string.Join(
                Environment.NewLine,
                Current.AudioStreams.Select(stream => $"{stream.Title}, {stream.Language}")));
        Console.WriteLine();
        Console.WriteLine($"Audio outputs count: {routes.Count}");
        Console.WriteLine(
            string.Join(Environment.NewLine, Current.AudioOutputs.Select(output => output.FriendlyName)));
            
        Console.WriteLine("Resulting routes:");
        Console.WriteLine(
            string.Join(
                Environment.NewLine,
                routes.Select(output => $"{output.First.Title} -> {output.Second.FriendlyName}")));
            
        Current.SetAudioRoutes(routes);
        SessionChanged?.Invoke(this);
    }
}