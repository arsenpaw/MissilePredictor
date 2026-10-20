using Microsoft.Extensions.Options;
using MissilePredictor.API.History;

namespace MissilePredictor.Tests.History;

public class HistoryOptionsValidatorTests
{
    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-01:00:00")]
    [InlineData("00:59:59")]
    public void Fails_WhenRetentionUnderOneHour(string retention)
    {
        var result = new HistoryOptionsValidator().Validate(null,
            new HistoryOptions { UnalertedTrackRetention = TimeSpan.Parse(retention) });

        Assert.True(result.Failed);
        Assert.Contains("History:UnalertedTrackRetention", result.FailureMessage);
    }

    [Theory]
    [InlineData("01:00:00")]
    [InlineData("06:00:00")]
    public void Passes_WhenRetentionAtLeastOneHour(string retention)
    {
        var result = new HistoryOptionsValidator().Validate(null,
            new HistoryOptions { UnalertedTrackRetention = TimeSpan.Parse(retention) });

        Assert.True(result.Succeeded);
    }
}
