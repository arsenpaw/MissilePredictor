using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MissilePredictor.API.History;

/// <summary>Creates the history database directory and applies pending migrations before other services start.</summary>
public class HistoryMigrationService(
    IDbContextFactory<HistoryDbContext> dbFactory,
    IOptions<HistoryOptions> options,
    ILogger<HistoryMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dataSource = new SqliteConnectionStringBuilder(options.Value.ConnectionString).DataSource;
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("History database ready at {DataSource}", dataSource);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
