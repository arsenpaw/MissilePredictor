using MissilePredictor.API.Areas;

namespace MissilePredictor.Tests.Areas;

public class AreaOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToArea_BlankFrom_FallsBackToTitle(string? from)
    {
        var area = new AreaOptions { Title = "Львів", From = from, Latitude = 49.8, Longitude = 24.0, RadiusKm = 10 }.ToArea();

        Assert.Equal("Львів", area.From);
    }

    [Fact]
    public void ToArea_KeepsGivenFrom()
    {
        var area = new AreaOptions { Title = "Львів", From = "Львову", Latitude = 49.8, Longitude = 24.0, RadiusKm = 10 }.ToArea();

        Assert.Equal("Львову", area.From);
    }
}
