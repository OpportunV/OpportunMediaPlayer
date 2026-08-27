using OMP.Lib.Subtitle;
using OMP.Ui.Models;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Models;

public class SubtitleRouteRowTests
{
    private static readonly SubtitleStream _english = new(1, "subrip", "English", "en", IsTextBased: true);

    [Fact]
    public void ZoneLabel_FollowsTheCurrentZoneName()
    {
        var row = new SubtitleRouteRow(_english, new SubtitleZone { Id = "z", Name = "Bottom" });

        Assert.Equal("Bottom", row.ZoneLabel);
    }

    [Fact]
    public void ReplacingTheZone_UpdatesTheLabelAndNotifies()
    {
        var row = new SubtitleRouteRow(_english, new SubtitleZone { Id = "z", Name = "Bottom" });
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.Zone = new SubtitleZone { Id = "z", Name = "Renamed" };

        Assert.Equal("Renamed", row.ZoneLabel);
        Assert.Contains(nameof(SubtitleRouteRow.ZoneLabel), raised);
    }

    [Fact]
    public void ReassigningTheSameZoneInstance_DoesNotNotify()
    {
        var zone = new SubtitleZone { Id = "z", Name = "Bottom" };
        var row = new SubtitleRouteRow(_english, zone);
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.Zone = zone;

        Assert.False(raised);
    }
}
