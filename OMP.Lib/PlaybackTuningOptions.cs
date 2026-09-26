namespace OMP.Lib;

public sealed class PlaybackTuningOptions
{
    public int AudioChannelCapacity { get; set; } = 200;

    public int VideoChannelCapacity { get; set; } = 10;

    public int NetworkVideoChannelCapacity { get; set; } = 2400;

    public int NetworkAudioChannelCapacity { get; set; } = 2400;

    public int SubtitleChannelCapacity { get; set; } = 32;

    public double FpsSampleWindowMs { get; set; } = 1000;

    public int BufferDurationSeconds { get; set; } = 2;

    public const string SectionName = "PlaybackTuning";
}
