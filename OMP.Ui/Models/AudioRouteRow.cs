using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OMP.Lib.Audio;

namespace OMP.Ui.Models;

internal sealed class AudioRouteRow(AudioRoute route, double volume, bool muted, double delayMs)
    : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AudioRoute Route { get; private set; } = route;

    public string OutputLabel { get; } = route.Output.FriendlyName;

    public bool CanDelete
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanDelete)));
        }
    }

    public bool Muted
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Muted)));
        }
    } = muted;

    public IReadOnlyList<AudioStreamOption> AvailableStreamOptions
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableStreamOptions)));
        }
    } = [];

    public AudioStreamOption? SelectedStreamOption
    {
        get => AvailableStreamOptions.FirstOrDefault(option => option.Stream.Id == Route.Stream.Id);
        set
        {
            if (value is null || value.Stream.Id == Route.Stream.Id)
            {
                return;
            }

            Route = Route with { Stream = value.Stream };
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedStreamOption)));
        }
    }

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

    public double DelayMs
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
