using System.ComponentModel;
using OMP.Lib.Audio.Output;

namespace OMP.Ui.Models;

internal sealed class OutputVolumeRow(AudioOutput output, double volume, bool muted) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AudioOutput Output { get; } = output;

    public string OutputLabel { get; } = output.FriendlyName;

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
}
