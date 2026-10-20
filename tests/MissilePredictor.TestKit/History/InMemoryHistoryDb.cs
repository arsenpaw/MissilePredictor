using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MissilePredictor.API.History;

namespace MissilePredictor.TestKit.History;

/// <summary>A migrated in-memory SQLite history database; the open connection keeps it alive until disposed.</summary>
public sealed class InMemoryHistoryDb : IDbContextFactory<HistoryDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<HistoryDbContext> _options;

    public InMemoryHistoryDb()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<HistoryDbContext>().UseSqlite(_connection).Options;

        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public HistoryDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
