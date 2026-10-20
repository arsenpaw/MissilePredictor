using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MissilePredictor.API.History;

/// <summary>
/// Creates the history database directory and applies pending migrations before other services start.
/// History is secondary: a failure is logged as critical and startup continues, so the Neptun stream and
/// Home Assistant alerts still run.
/// </summary>
public class HistoryMigrationService(
    IDbContextFactory<HistoryDbContext> dbFactory,
    IOptions<HistoryOptions> options,
    ILogger<HistoryMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? dataSource = null;
        try
        {
            dataSource = new SqliteConnectionStringBuilder(options.Value.ConnectionString).DataSource;
            var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("History database ready at {DataSource}", dataSource);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex,
                "History database at {DataSource} is unusable; history will not be recorded. Neptun alerts keep running",
                dataSource);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
