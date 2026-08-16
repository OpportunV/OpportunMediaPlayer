namespace OMP.Ui.Settings;

public sealed class OutputVolumeSetting
{
    public string FriendlyName { get; set; } = string.Empty;

    public double Volume { get; set; } = 1.0;

    public bool Muted { get; set; }

    public double DelayMs { get; set; }
}
