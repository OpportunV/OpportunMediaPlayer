using System.Buffers.Binary;
using System.Text;

namespace OMP.Lib.Interop;

/// <summary>
/// Walks just enough of an MP4/MOV box tree (<c>moov/trak/{tkhd,udta/name,mdia/elng}</c>) to find
/// <see cref="Mp4TrackMetadata"/>, keyed by <c>tkhd</c> track ID - which FFmpeg's demuxer copies into
/// <c>AVStream.id</c>, so a lookup by <c>stream->id</c> is exact. Only box headers are read on the
/// way to <c>moov</c> (an <c>mdat</c> is skipped by seeking, never read), so it's cheap even when
/// <c>moov</c> sits at the end of the file.
/// </summary>
internal static class Mp4TrackMetadataReader
{
    /// <summary>
    /// Caps a single string box so a corrupt size can't make us allocate the rest of the file.
    /// </summary>
    private const int MaxStringBoxBytes = 4096;

    /// <summary>
    /// Returns an empty map, rather than throwing, for anything it can't make sense of: this is a
    /// best-effort supplement to FFmpeg's own metadata, never a reason for a file to fail to open.
    /// </summary>
    public static IReadOnlyDictionary<int, Mp4TrackMetadata> Read(Stream stream)
    {
        var tracks = new Dictionary<int, Mp4TrackMetadata>();
        try
        {
            foreach (var moov in Children(stream, 0, stream.Length).Where(b => b.Type == "moov"))
            {
                foreach (var trak in Children(stream, moov.PayloadStart, moov.End).Where(b => b.Type == "trak"))
                {
                    ReadTrack(stream, trak, tracks);
                }
            }
        }
        catch (IOException)
        {
            // Truncated or unreadable file: keep whatever was collected before the failure.
        }

        return tracks;
    }

    private static void ReadTrack(Stream stream, Box trak, Dictionary<int, Mp4TrackMetadata> tracks)
    {
        int? trackId = null;
        string? name = null;
        string? language = null;

        foreach (var child in Children(stream, trak.PayloadStart, trak.End))
        {
            switch (child.Type)
            {
                case "tkhd":
                    trackId = ReadTrackId(stream, child);
                    break;
                case "udta":
                    var nameBox = Children(stream, child.PayloadStart, child.End).FirstOrDefault(b => b.Type == "name");
                    name = nameBox is null ? null : ReadString(stream, nameBox.PayloadStart, nameBox.End);
                    break;
                case "mdia":
                    // elng is a FullBox: 4 bytes of version/flags, then a NUL-terminated string.
                    var elngBox = Children(stream, child.PayloadStart, child.End).FirstOrDefault(b => b.Type == "elng");
                    language = elngBox is null ? null : ReadString(stream, elngBox.PayloadStart + 4, elngBox.End);
                    break;
            }
        }

        if (trackId is { } id && (name is not null || language is not null))
        {
            tracks[id] = new(name, language);
        }
    }

    /// <summary>
    /// <c>tkhd</c> is a FullBox whose track ID follows two timestamps: 32-bit in version 0, 64-bit in version 1.
    /// </summary>
    private static int? ReadTrackId(Stream stream, Box tkhd)
    {
        Span<byte> versionAndFlags = stackalloc byte[4];
        if (!TryReadAt(stream, tkhd.PayloadStart, versionAndFlags))
        {
            return null;
        }

        var trackIdOffset = tkhd.PayloadStart + 4 + (versionAndFlags[0] == 1 ? 16 : 8);
        Span<byte> trackId = stackalloc byte[4];
        return trackIdOffset + 4 <= tkhd.End && TryReadAt(stream, trackIdOffset, trackId)
            ? (int)BinaryPrimitives.ReadUInt32BigEndian(trackId)
            : null;
    }

    private static string? ReadString(Stream stream, long start, long end)
    {
        var length = end - start;
        if (length <= 0 || length > MaxStringBoxBytes)
        {
            return null;
        }

        var buffer = new byte[length];
        if (!TryReadAt(stream, start, buffer))
        {
            return null;
        }

        var value = Encoding.UTF8.GetString(buffer).Split('\0')[0].Trim();
        return value.Length > 0 ? value : null;
    }

    private static bool TryReadAt(Stream stream, long position, Span<byte> buffer)
    {
        if (position < 0 || position + buffer.Length > stream.Length)
        {
            return false;
        }

        stream.Position = position;
        stream.ReadExactly(buffer);
        return true;
    }

    /// <summary>
    /// The boxes directly inside <c>[start, end)</c>. Stops at the first header that doesn't fit its
    /// parent rather than guessing past it.
    /// </summary>
    private static IEnumerable<Box> Children(Stream stream, long start, long end)
    {
        var position = start;
        var header = new byte[16];

        while (position + 8 <= end)
        {
            if (!TryReadAt(stream, position, header.AsSpan(0, 8)))
            {
                yield break;
            }

            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = Encoding.Latin1.GetString(header, 4, 4);
            var headerLength = 8;

            if (size == 1)
            {
                if (!TryReadAt(stream, position + 8, header.AsSpan(8, 8)))
                {
                    yield break;
                }

                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
                headerLength = 16;
            }
            else if (size == 0)
            {
                // "Extends to the end of its parent" - only legal for the last box.
                size = end - position;
            }

            if (size < headerLength || size > end - position)
            {
                yield break;
            }

            yield return new(type, position + headerLength, position + size);
            position += size;
        }
    }

    private sealed record Box(string Type, long PayloadStart, long End);
}
