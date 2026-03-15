namespace OMP.Lib.Video;

public sealed record VideoFrame(int Width, int Height, int Stride, byte[] Data);