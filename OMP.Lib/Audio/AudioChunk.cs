namespace OMP.Lib.Audio;

    internal readonly record struct AudioChunk(byte[] Data, int Length, double TimeSeconds);