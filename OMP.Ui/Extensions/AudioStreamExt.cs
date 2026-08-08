using OMP.Lib.Audio;

namespace OMP.Ui.Extensions;

public static class AudioStreamExt
{
    extension(AudioStream stream)
    {
        public string Describe() => $"{stream.Title} [{stream.Language}] ({stream.Codec})";
    }
}