using System.ComponentModel;
using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Models;

internal sealed class SubtitleRouteRow(SubtitleStream stream, SubtitleZone zone) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public SubtitleStream Stream { get; } = stream;

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

    public string StreamLabel { get; } = stream.Describe();

    public string ZoneLabel => Zone.Name;
}
