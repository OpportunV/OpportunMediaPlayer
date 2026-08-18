using System.Collections.Generic;

namespace OMP.Ui.Settings;

public sealed class UserSettings
{
    public int Version { get; set; } = CurrentVersion;

    public double MasterVolume { get; set; } = 1.0;

    public bool IsMuted { get; set; }

    public double PlaybackSpeed { get; set; } = 1.0;

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public string? Language { get; set; }

    public List<OutputVolumeSetting> OutputVolumes { get; set; } = [];

    public List<PreferredAudioTrackSetting> PreferredAudioTracks { get; set; } = [];

    public List<SubtitleZone> SubtitleZones { get; set; } = SubtitleZone.CreateBuiltIns();

    public WindowSettings Window { get; set; } = new();

    public const int CurrentVersion = 1;
}
