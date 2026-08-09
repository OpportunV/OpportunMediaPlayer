using System.Collections.Generic;

namespace OMP.Ui.Settings;

public sealed class UserSettings
{
    public int Version { get; set; } = CurrentVersion;

    public double MasterVolume { get; set; } = 1.0;

    public bool IsMuted { get; set; }

    public double PlaybackSpeed { get; set; } = 1.0;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public List<OutputVolumeSetting> OutputVolumes { get; set; } = [];

    public List<string> PreferredAudioOutputs { get; set; } = [];

    public List<SubtitleZone> SubtitleZones { get; set; } = SubtitleZone.CreateBuiltIns();

    public const int CurrentVersion = 1;
}
