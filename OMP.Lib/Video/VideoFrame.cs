namespace OMP.Lib.Video;

public readonly record struct VideoFrame(int Width, int Height, int Stride, nint DataPtr, int DataLength, double TimeSeconds);