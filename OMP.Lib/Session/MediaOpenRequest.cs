namespace OMP.Lib.Session;

public sealed record MediaOpenRequest(string PrimarySource, IReadOnlyList<AudioSidecarSource> AudioSidecars)
{
    public static MediaOpenRequest ForFile(string path) => new(path, []);
}
