using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Settings;

public class SubtitleZoneTests
{
    [Fact]
    public void CreateBuiltIns_ReturnsTopAndBottomZones()
    {
        var zones = SubtitleZone.CreateBuiltIns();

        Assert.Equal(2, zones.Count);
        Assert.Contains(zones, z => z.Id == SubtitleZone.BuiltInTopId);
        Assert.Contains(zones, z => z.Id == SubtitleZone.BuiltInBottomId);
        Assert.All(zones, z => Assert.True(z.IsBuiltIn));
    }

    [Fact]
    public void IsDeletable_BuiltInZone_ReturnsFalse()
    {
        var zone = SubtitleZone.CreateBuiltIns().First();

        Assert.False(zone.IsDeletable);
    }

    [Fact]
    public void IsDeletable_CustomZone_ReturnsTrue()
    {
        var zone = new SubtitleZone();

        Assert.True(zone.IsDeletable);
    }

    [Fact]
    public void Clone_ReturnsDistinctInstanceWithEqualValues()
    {
        var zone = new SubtitleZone { Name = "Custom", X = 0.2, Width = 0.5 };

        var clone = zone.Clone();

        Assert.NotSame(zone, clone);
        Assert.Equal(zone.Name, clone.Name);
        Assert.Equal(zone.X, clone.X);
        Assert.Equal(zone.Width, clone.Width);
    }

    [Fact]
    public void Clone_MutatingClone_DoesNotAffectOriginal()
    {
        var zone = new SubtitleZone { Name = "Custom" };
        var clone = zone.Clone();

        clone.Name = "Renamed";

        Assert.Equal("Custom", zone.Name);
        Assert.Equal("Renamed", clone.Name);
    }
}
