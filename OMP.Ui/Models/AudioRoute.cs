using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;

namespace OMP.Ui.Models;

public record AudioRoute(AudioStream AudionStream, AudioOutput AudioOutput)
{
    public string Stream => AudionStream.Title;

    public string Output => AudioOutput.FriendlyName;
}