using MissilePredictor.API.History;
using MissilePredictor.API.Neptun.Models;
using MissilePredictor.TestKit;
using static MissilePredictor.TestKit.Fakes.FakeNeptunServer;

namespace MissilePredictor.Tests.History;

public class NeptunHistoryRecorderTests
{
    private static List<object> Drain(NeptunHistoryRecorder recorder)
    {
        var items = new List<object>();
        while (recorder.Pending.TryRead(out var item)) items.Add(item);
        return items;
    }

    [Fact]
    public void Recorder_EmitsTrackPointForPositionedThreat()
    {
        var recorder = new NeptunHistoryRecorder();
        var updatedAt = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(3));
        var threat = new NeptunThreat
        {
            Id = "trk_1", Type = "uav", Title = "БпЛА", Status = "active",
            Lat = InsideLat, Lon = InsideLon, Heading = 90, UpdatedAt = updatedAt
        };

        recorder.RecordPosition(threat);

        var point = Assert.IsType<NeptunTrackPoint>(Assert.Single(Drain(recorder)));
        Assert.Equal("trk_1", point.ThreatId);
        Assert.Equal("active", point.Status);
        Assert.Equal(InsideLat, point.Lat);
        Assert.Equal(90, point.Heading);
        Assert.Equal(updatedAt.UtcDateTime, point.UpdatedAt);
    }

    [Fact]
    public void Recorder_SkipsAreaOnlyThreat()
    {
        var recorder = new NeptunHistoryRecorder();

        recorder.RecordPosition(new NeptunThreat { Id = "trk_1", AreaOnly = true, Lat = InsideLat, Lon = InsideLon });

        Assert.Empty(Drain(recorder));
    }

    [Fact]
    public void Recorder_SkipsUnchangedPosition()
    {
        var recorder = new NeptunHistoryRecorder();
        var threat = Threat("trk_1", InsideLat, InsideLon);

        recorder.RecordPosition(threat);
        recorder.RecordPosition(threat);
        Assert.Single(Drain(recorder));

        recorder.RecordPosition(Threat("trk_1", InsideLat, InsideLon + 0.01));
        Assert.Single(Drain(recorder));

        recorder.Forget("trk_1");
        recorder.RecordPosition(Threat("trk_1", InsideLat, InsideLon + 0.01));
        Assert.Single(Drain(recorder));
    }

    [Fact]
    public void Recorder_EmitsAlertWithDistance()
    {
        var recorder = new NeptunHistoryRecorder();
        var threat = Threat("trk_1", InsideLat, InsideLon);

        recorder.RecordAlert(TestAreas.Lviv, threat, false, "HA down");

        var alert = Assert.IsType<NeptunAlert>(Assert.Single(Drain(recorder)));
        Assert.Equal("Львів", alert.AreaTitle);
        Assert.Equal("trk_1", alert.ThreatId);
        Assert.Equal(TestAreas.Lviv.DistanceKm(InsideLat, InsideLon), alert.DistanceKm, 3);
        Assert.False(alert.AlertSent);
        Assert.Equal("HA down", alert.Error);
    }

    [Fact]
    public void Recorder_ReplacesJsonNullStringsWithEmpty()
    {
        // One null from the stream must not fail the database batch it lands in (the columns are NOT NULL).
        var recorder = new NeptunHistoryRecorder();
        var threat = new NeptunThreat { Id = null!, Type = null!, Title = null!, Status = null!, Lat = InsideLat, Lon = InsideLon };

        recorder.RecordPosition(threat);
        recorder.RecordAlert(TestAreas.Lviv, threat, true, null);

        var items = Drain(recorder);
        var point = Assert.IsType<NeptunTrackPoint>(items[0]);
        var alert = Assert.IsType<NeptunAlert>(items[1]);
        Assert.Equal(("", "", "", ""), (point.ThreatId, point.Type, point.Title, point.Status));
        Assert.Equal(("", "", ""), (alert.ThreatId, alert.Type, alert.Title));
    }
}
