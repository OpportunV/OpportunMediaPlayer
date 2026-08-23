namespace OMP.Lib.Subtitle;

public sealed record SubtitleSidecarSource(
    string Url,
    string? Language = null,
    string? Title = null,
    IReadOnlyDictionary<string, string>? Headers = null);
