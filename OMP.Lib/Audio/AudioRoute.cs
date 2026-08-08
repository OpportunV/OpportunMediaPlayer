using OMP.Lib.Audio.Output;

namespace OMP.Lib.Audio;

public sealed record AudioRoute(AudioStream Stream, AudioOutput Output);
