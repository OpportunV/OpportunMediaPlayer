using System.ComponentModel;
using OMP.Lib.Audio;
using OMP.Ui.Extensions;

namespace OMP.Ui.Models;

internal sealed class AudioRouteRow(AudioRoute route, double volume, bool muted, double? delayMs)
    : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AudioRoute Route { get; } = route;

    public string StreamLabel { get; } = route.Stream.Describe();

    public string OutputLabel { get; } = route.Output.FriendlyName;

    public bool CanDelete { get; set; }

    public bool Muted { get; set; } = muted;

    public double Volume
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
        }
    } = volume;

    public double? DelayMs
    {
        get;
        set
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayMs)));
        }
    } = delayMs;
}
