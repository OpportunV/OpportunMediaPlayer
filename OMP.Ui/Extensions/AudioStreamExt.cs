using OMP.Lib.Audio;

namespace OMP.Ui.Extensions;

public static class AudioStreamExt
{
    extension(AudioStream stream)
    {
        public string Describe() => $"[{stream.Language}] {stream.Title} ({stream.Codec})";
    }
}