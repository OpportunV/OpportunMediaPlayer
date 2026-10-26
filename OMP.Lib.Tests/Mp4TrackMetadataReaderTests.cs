using System.Buffers.Binary;
using System.Text;
using OMP.Lib.Interop;

namespace OMP.Lib.Tests;

public class Mp4TrackMetadataReaderTests
{
    [Fact]
    public void Read_GivenTrackNameAndElng_KeysThemByTrackId()
    {
        var file = Concat(
            Box("ftyp", Ascii("isom")),
            Box("moov",
                Box("trak", TkhdV0(1), Box("mdia", Box("hdlr", new byte[24]))),
                Box("trak",
                    TkhdV0(2),
                    Box("mdia", Elng("pt-BR")),
                    Box("udta", Box("name", Utf8("Áudio original"))))));

        var tracks = Read(file);

        Assert.Equal(new Mp4TrackMetadata("Áudio original", "pt-BR"), Assert.Single(tracks, t => t.Key == 2).Value);
        Assert.DoesNotContain(1, tracks.Keys);
    }

    [Fact]
    public void Read_GivenVersion1Tkhd_ReadsTrackIdAfter64BitTimestamps()
    {
        var file = Box("moov", Box("trak", TkhdV1(7), Box("udta", Box("name", Utf8("Commentary")))));

        Assert.Equal("Commentary", Read(file)[7].Name);
    }

    [Fact]
    public void Read_GivenMoovAfterLargeMdat_SkipsMdatWithoutReadingIt()
    {
        // A 64-bit-size mdat (size field 1) claiming 1 MiB of payload, written sparse: the reader must
        // skip it by its declared size, not scan through it.
        const long mdatPayload = 1 << 20;
        var mdatHeader = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(mdatHeader, 1);
        Encoding.ASCII.GetBytes("mdat").CopyTo(mdatHeader, 4);
        BinaryPrimitives.WriteUInt64BigEndian(mdatHeader.AsSpan(8), 16 + mdatPayload);

        using var stream = new MemoryStream();
        stream.Write(mdatHeader);
        stream.SetLength(16 + mdatPayload);
        stream.Position = stream.Length;
        stream.Write(Box("moov", Box("trak", TkhdV0(3), Box("udta", Box("name", Utf8("Late moov"))))));

        Assert.Equal("Late moov", Mp4TrackMetadataReader.Read(stream)[3].Name);
    }

    [Fact]
    public void Read_GivenNulPaddedName_TrimsIt()
    {
        var file = Box("moov", Box("trak", TkhdV0(1), Box("udta", Box("name", Utf8("Stereo\0\0")))));

        Assert.Equal("Stereo", Read(file)[1].Name);
    }

    [Fact]
    public void Read_GivenLastBoxWithSizeZero_TreatsItAsExtendingToTheEnd()
    {
        var moov = Box("moov", Box("trak", TkhdV0(1), Box("udta", Box("name", Utf8("Open-ended")))));
        BinaryPrimitives.WriteUInt32BigEndian(moov, 0);

        Assert.Equal("Open-ended", Read(moov)[1].Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(40)]
    public void Read_GivenTruncatedFile_ReturnsWhatItCanWithoutThrowing(int cut)
    {
        var file = Box("moov", Box("trak", TkhdV0(1), Box("udta", Box("name", Utf8("Truncated")))));

        var tracks = Read(file[..^cut]);

        Assert.True(tracks.Count == 0 || tracks[1].Name == "Truncated");
    }

    [Fact]
    public void Read_GivenNonMp4Data_ReturnsEmpty()
    {
        Assert.Empty(Read(Encoding.ASCII.GetBytes("ID3\u0004 this is an mp3, not an mp4 box tree at all")));
    }

    private static IReadOnlyDictionary<int, Mp4TrackMetadata> Read(byte[] file) =>
        Mp4TrackMetadataReader.Read(new MemoryStream(file));

    private static byte[] Box(string type, params byte[][] children)
    {
        var payload = Concat(children);
        var box = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.Latin1.GetBytes(type).CopyTo(box, 4);
        payload.CopyTo(box, 8);
        return box;
    }

    private static byte[] TkhdV0(uint trackId)
    {
        var payload = new byte[4 + 8 + 4 + 4 + 60];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12), trackId);
        return Box("tkhd", payload);
    }

    private static byte[] TkhdV1(uint trackId)
    {
        var payload = new byte[4 + 16 + 4 + 4 + 64];
        payload[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20), trackId);
        return Box("tkhd", payload);
    }

    private static byte[] Elng(string tag) => Box("elng", new byte[4], Ascii(tag + "\0"));

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
