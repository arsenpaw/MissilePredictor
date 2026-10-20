using System.Text.Json;
using MissilePredictor.API.Areas;
using MissilePredictor.API.Neptun.Models;
using MissilePredictor.TestKit;

namespace MissilePredictor.Tests.Areas;

public class AreaThreatTrackerTests
{
    // ~4.5 km north of Lviv centre, and Kyiv (~470 km away).
    private const double InsideLat = 49.88, InsideLon = 24.03;
    private const double KyivLat = 50.4501, KyivLon = 30.5234;

    [Fact]
    public void Lviv_ContainsNearbyPoints_AndNotKyiv()
    {
        Assert.True(TestAreas.Lviv.Contains(TestAreas.Lviv.Latitude, TestAreas.Lviv.Longitude));
        Assert.True(TestAreas.Lviv.Contains(InsideLat, InsideLon));
        Assert.False(TestAreas.Lviv.Contains(KyivLat, KyivLon));
        Assert.InRange(TestAreas.Lviv.DistanceKm(KyivLat, KyivLon), 460, 480);
    }

    [Fact]
    public void TryEnter_ReportsTargetOnceWhenItEnters()
    {
        var tracker = new AreaThreatTracker(TestAreas.Lviv);

        Assert.False(tracker.TryEnter(Threat("trk_1", KyivLat, KyivLon)));
        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));
        Assert.False(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon + 0.01)));
    }

    [Fact]
    public void TryEnter_ReportsAgainAfterTargetLeavesAndReturns()
    {
        var tracker = new AreaThreatTracker(TestAreas.Lviv);

        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));
        Assert.False(tracker.TryEnter(Threat("trk_1", KyivLat, KyivLon)));
        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));
    }

    [Fact]
    public void TryEnter_IgnoresAdvisoryAreaOnlyAndInactiveTargets()
    {
        var tracker = new AreaThreatTracker(TestAreas.Lviv);

        Assert.False(tracker.TryEnter(Threat("advisory", InsideLat, InsideLon, advisory: true)));
        Assert.False(tracker.TryEnter(Threat("area-only", InsideLat, InsideLon, areaOnly: true)));
        Assert.False(tracker.TryEnter(Threat("stale", InsideLat, InsideLon, status: "stale")));
        Assert.False(tracker.TryEnter(Threat("resolved", InsideLat, InsideLon, status: "resolved")));

        // A stale track that becomes active again inside the area is a real entry.
        Assert.True(tracker.TryEnter(Threat("stale", InsideLat, InsideLon)));
    }

    [Fact]
    public void Remove_ForgetsTarget()
    {
        var tracker = new AreaThreatTracker(TestAreas.Lviv);

        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));
        tracker.Remove("trk_1");
        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));
    }

    [Fact]
    public void ApplySnapshot_ReportsInsideTargets_AndStaysQuietOnReconnect()
    {
        var tracker = new AreaThreatTracker(TestAreas.Lviv);
        var snapshot = new[] { Threat("in", InsideLat, InsideLon), Threat("out", KyivLat, KyivLon) };

        Assert.Equal(["in"], tracker.ApplySnapshot(snapshot).Select(t => t.Id));
        Assert.Empty(tracker.ApplySnapshot(snapshot));
    }

    [Fact]
    public void ApplySnapshot_DropsTargetsMissingFromIt()
    {
        var tracker = new AreaThreatTracker(TestAreas.Lviv);
        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));

        // trk_1 vanished while we were disconnected, so its next appearance is a fresh entry.
        tracker.ApplySnapshot([]);
        Assert.True(tracker.TryEnter(Threat("trk_1", InsideLat, InsideLon)));
    }

    [Fact]
    public void RealUpsertFrame_Deserializes()
    {
        // Captured from wss://neptun.in.ua/api/v1/stream.
        const string frame = """
            {"type":"upsert","ts":"2026-10-18T17:50:26.052Z","data":{"id":"trk_00279185","type":"uav","title":"БпЛА","region":"Чернігівська область","district":"","locality":"Воловики","lat":51.806935660259384,"lon":31.975046346921584,"heading":152.91,"confidenceLevel":"medium","sourceCount":1,"updatedAt":"2026-10-18T17:49:54Z","explanationShort":"БпЛА курсом на Воловики. Підтверджень: 1.","status":"active","confirmedAt":"2026-10-18T17:49:54Z","uncertaintyKm":4,"positionQuality":"approx","lifecycle":"uncertain","displayConfidence":"medium","destination":true,"presumptiveCourse":true}}
            """;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var envelope = JsonSerializer.Deserialize<NeptunEnvelope>(frame, options)!;
        var threat = envelope.Data.Deserialize<NeptunThreat>(options)!;

        Assert.Equal("upsert", envelope.Type);
        Assert.Equal("trk_00279185", threat.Id);
        Assert.Equal("uav", threat.Type);
        Assert.Equal("active", threat.Status);
        Assert.Equal(51.806935660259384, threat.Lat);
        Assert.Equal(152.91, threat.Heading);
        Assert.False(threat.Advisory);
        Assert.False(threat.AreaOnly);
    }

    private static NeptunThreat Threat(
        string id, double lat, double lon, string status = "active", bool advisory = false, bool areaOnly = false) => new()
    {
        Id = id,
        Type = "uav",
        Title = "БпЛА",
        Lat = lat,
        Lon = lon,
        Status = status,
        Advisory = advisory,
        AreaOnly = areaOnly
    };
}
