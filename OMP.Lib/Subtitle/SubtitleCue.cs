namespace OMP.Lib.Subtitle;

public sealed record SubtitleCue(string ZoneId, IReadOnlyList<SubtitleLine> Lines, double StartSeconds, double EndSeconds);
