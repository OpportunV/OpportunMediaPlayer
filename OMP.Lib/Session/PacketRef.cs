using FFmpeg.AutoGen;

namespace OMP.Lib.Session;

internal unsafe struct PacketRef
{
    public AVPacket* Packet;
}