using Microsoft.Extensions.Options;

namespace MissilePredictor.API.History;

/// <summary>Rejects a retention so short that the hourly cleanup would purge tracks that are still live.</summary>
public sealed class HistoryOptionsValidator : IValidateOptions<HistoryOptions>
{
    private static readonly TimeSpan MinRetention = TimeSpan.FromHours(1);

    public ValidateOptionsResult Validate(string? name, HistoryOptions options) =>
        options.UnalertedTrackRetention >= MinRetention
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"History:UnalertedTrackRetention must be at least {MinRetention} (got {options.UnalertedTrackRetention}).");
}
