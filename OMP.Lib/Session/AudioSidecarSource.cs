namespace OMP.Lib.Session;

public sealed record AudioSidecarSource(
    string Url,
    string? Language = null,
    string? Title = null,
    IReadOnlyDictionary<string, string>? Headers = null);