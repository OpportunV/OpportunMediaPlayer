using FFmpeg.AutoGen;

namespace OMP.Lib.Session;

internal readonly unsafe struct PacketRef(AVPacket* packet, int generation)
{
    public AVPacket* Packet { get; } = packet;

    public int Generation { get; } = generation;
}