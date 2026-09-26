namespace OMP.Lib;

public sealed class PlaybackTuningOptions
{
    public int AudioChannelCapacity { get; set; } = 200;

    public int VideoChannelCapacity { get; set; } = 10;

    /// <summary>
    /// Network sources read up to 30 seconds ahead of playback to ride out stalls, so their packet
    /// channels must hold that many packets (~60 fps video, ~50 packets/s audio, plus headroom).
    /// The audio channel drops its oldest packet when full, so too small a value here is audible.
    /// </summary>
    public int NetworkVideoChannelCapacity { get; set; } = 2400;

    public int NetworkAudioChannelCapacity { get; set; } = 2400;

    public int SubtitleChannelCapacity { get; set; } = 32;

    public double FpsSampleWindowMs { get; set; } = 1000;

    public int BufferDurationSeconds { get; set; } = 2;

    public const string SectionName = "PlaybackTuning";
}
