using MissilePredictor.API.History;

namespace MissilePredictor.Tests.History;

public class KyivTimeTests
{
    [Fact]
    public void DayStartUtc_SummerDay_IsThreeHoursBeforeUtcMidnight()
    {
        var start = KyivTime.DayStartUtc(new DateTime(2026, 10, 21));

        Assert.Equal(new DateTime(2026, 10, 20, 21, 0, 0), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public void DayStartUtc_WinterDay_IsTwoHoursBeforeUtcMidnight()
    {
        var start = KyivTime.DayStartUtc(new DateTime(2026, 1, 15, 13, 45, 0));

        Assert.Equal(new DateTime(2026, 1, 14, 22, 0, 0), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }
}
