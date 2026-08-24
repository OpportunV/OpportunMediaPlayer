using FFmpeg.AutoGen;

namespace OMP.Lib.Session;

internal readonly unsafe struct PacketRef(AVPacket* packet, int generation, int sourceId)
{
    public AVPacket* Packet { get; } = packet;

    public int Generation { get; } = generation;

    public int SourceId { get; } = sourceId;
}