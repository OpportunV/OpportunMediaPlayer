using OMP.Lib.Subtitle;

namespace OMP.Lib.Session;

public sealed record MediaOpenRequest(
    string PrimarySource,
    IReadOnlyList<AudioSidecarSource> AudioSidecars,
    IReadOnlyDictionary<string, string>? PrimaryHeaders = null,
    IReadOnlyList<SubtitleSidecarSource>? SubtitleSidecars = null)
{
    public IReadOnlyList<SubtitleSidecarSource> SubtitleSidecars { get; } = SubtitleSidecars ?? [];

    public static MediaOpenRequest ForFile(string path) => new(path, []);
}
