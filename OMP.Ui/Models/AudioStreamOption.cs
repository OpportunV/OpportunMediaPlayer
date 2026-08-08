using OMP.Lib.Audio;
using OMP.Ui.Extensions;

namespace OMP.Ui.Models;

internal sealed class AudioStreamOption(AudioStream stream)
{
    public AudioStream Stream { get; } = stream;

    public string Label { get; } = stream.Describe();
}
