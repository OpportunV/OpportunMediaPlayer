namespace OMP.Lib.Session;

public sealed record MediaOpenRequest(
    string PrimarySource,
    IReadOnlyList<AudioSidecarSource> AudioSidecars,
    IReadOnlyDictionary<string, string>? PrimaryHeaders = null)
{
    public static MediaOpenRequest ForFile(string path) => new(path, []);
}
