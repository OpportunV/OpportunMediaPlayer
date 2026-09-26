using OMP.Lib.Subtitle;
using OMP.Ui.Models;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Models;

public class SubtitleRouteRowTests
{
    private static readonly SubtitleStreamOption _english = new(new SubtitleStream(1, "subrip", "English", "en", IsTextBased: true));
    private static readonly SubtitleStreamOption _french = new(new SubtitleStream(2, "subrip", "French", "fr", IsTextBased: true));

    [Fact]
    public void ZoneLabel_FollowsTheCurrentZoneName()
    {
        var row = new SubtitleRouteRow(new SubtitleZone { Id = "z", Name = "Bottom" }, _english);

        Assert.Equal("Bottom", row.ZoneLabel);
    }

    [Fact]
    public void ReplacingTheZone_UpdatesTheLabelAndNotifies()
    {
        var row = new SubtitleRouteRow(new SubtitleZone { Id = "z", Name = "Bottom" }, _english);
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
        var row = new SubtitleRouteRow(zone, _english);
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.Zone = zone;

        Assert.False(raised);
    }

    [Fact]
    public void SwappingTheTrack_UpdatesTheStreamAndNotifies()
    {
        var row = new SubtitleRouteRow(new SubtitleZone { Id = "z", Name = "Bottom" }, _english);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.SelectedStreamOption = _french;

        Assert.Equal(_french.Stream, row.Stream);
        Assert.Contains(nameof(SubtitleRouteRow.SelectedStreamOption), raised);
        Assert.Contains(nameof(SubtitleRouteRow.Stream), raised);
    }

    [Fact]
    public void ClearingOrReselectingTheSameTrack_IsIgnored()
    {
        var row = new SubtitleRouteRow(new SubtitleZone { Id = "z", Name = "Bottom" }, _english);
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.SelectedStreamOption = null;
        row.SelectedStreamOption = new SubtitleStreamOption(_english.Stream);

        Assert.False(raised);
        Assert.Same(_english, row.SelectedStreamOption);
    }
}
