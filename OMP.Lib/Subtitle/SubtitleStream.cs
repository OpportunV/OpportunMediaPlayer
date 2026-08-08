namespace OMP.Lib.Subtitle;

public sealed record SubtitleStream(int Id, string Codec, string Title, string Language, bool IsTextBased);
