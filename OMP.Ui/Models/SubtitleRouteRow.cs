using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Models;

internal sealed class SubtitleRouteRow(SubtitleStream stream, SubtitleZone zone)
{
    public SubtitleStream Stream { get; } = stream;

    public SubtitleZone Zone { get; } = zone;

    public string StreamLabel { get; } = stream.Describe();

    public string ZoneLabel { get; } = zone.Name;
}
