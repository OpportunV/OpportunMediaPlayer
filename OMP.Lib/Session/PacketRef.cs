using FFmpeg.AutoGen;

namespace OMP.Lib.Session;

public unsafe struct PacketRef
{
    public AVPacket* Packet;
}