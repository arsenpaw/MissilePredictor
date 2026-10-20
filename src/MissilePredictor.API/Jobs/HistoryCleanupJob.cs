using Hangfire.Console;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MissilePredictor.API.History;

namespace MissilePredictor.API.Jobs;

// Drops track points of threats that never triggered an alert once they are older than the retention window.
public class HistoryCleanupJob
{
    private readonly IDbContextFactory<HistoryDbContext> _dbFactory;
    private readonly HistoryOptions _options;
    private readonly ILogger<HistoryCleanupJob> _logger;

    public HistoryCleanupJob(
        IDbContextFactory<HistoryDbContext> dbFactory,
        IOptions<HistoryOptions> options,
        ILogger<HistoryCleanupJob> logger)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - _options.UnalertedTrackRetention;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.TrackPoints
            .Where(p => p.ReceivedAt < cutoff && !db.Alerts.Any(a => a.ThreatId == p.ThreatId))
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("History cleanup removed {Count} track point(s) of unalerted threats older than {Cutoff:u}", deleted, cutoff);
        return deleted;
    }

    public async Task Execute(PerformContext context)
    {
        var deleted = await PurgeAsync(context.CancellationToken.ShutdownToken);
        context.WriteLine($"Removed {deleted} track point(s) of unalerted threats.");
    }
}
