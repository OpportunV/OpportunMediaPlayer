namespace OMP.Lib.Interop;

/// <summary>
/// Per-track metadata FFmpeg 7.1.1's MP4/MOV demuxer never exposes: <see cref="Name"/> from the
/// track's <c>udta/name</c> box (where FFmpeg's own muxer, mp4v2/HandBrake and others write a track
/// title - the 7.1.1 demuxer skips that box outright) and <see cref="Language"/> from
/// <c>mdia/elng</c>, the full BCP-47 tag (<c>en-US</c>, <c>pt-BR</c>) that the 3-letter
/// <c>mdhd</c> code FFmpeg does read can't express.
/// </summary>
internal sealed record Mp4TrackMetadata(string? Name, string? Language);
