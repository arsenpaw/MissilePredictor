using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MissilePredictor.API.History;

namespace MissilePredictor.TestKit.History;

/// <summary>
/// A migrated in-memory SQLite history database. Every created context gets its own connection to one
/// uniquely named shared-cache database (SqliteConnection is not thread-safe); a keeper connection holds the database alive until disposed.
/// </summary>
public sealed class InMemoryHistoryDb : IDbContextFactory<HistoryDbContext>, IDisposable
{
    private readonly SqliteConnection _keeper;
    private readonly DbContextOptions<HistoryDbContext> _options;

    public InMemoryHistoryDb()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"history-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _options = new DbContextOptionsBuilder<HistoryDbContext>().UseSqlite(connectionString).Options;

        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public HistoryDbContext CreateDbContext() => new(_options);

    public void Dispose() => _keeper.Dispose();
}
