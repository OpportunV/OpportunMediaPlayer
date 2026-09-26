using System.Collections.Generic;
using System.ComponentModel;
using OMP.Lib.Subtitle;
using OMP.Ui.Settings;

namespace OMP.Ui.Models;

internal sealed class SubtitleRouteRow(SubtitleZone zone, SubtitleStreamOption streamOption) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SubtitleZone Zone
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Zone)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ZoneLabel)));
        }
    } = zone;

    public string ZoneLabel => Zone.Name;

    public IReadOnlyList<SubtitleStreamOption> AvailableStreamOptions
    {
        get;
        set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableStreamOptions)));
        }
    } = [];

    public SubtitleStreamOption? SelectedStreamOption
    {
        get;
        set
        {
            if (value is null || value.Stream.Id == field?.Stream.Id)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedStreamOption)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stream)));
        }
    } = streamOption;

    public SubtitleStream Stream => SelectedStreamOption!.Stream;
}
