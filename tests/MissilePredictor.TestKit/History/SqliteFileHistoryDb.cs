using Microsoft.EntityFrameworkCore;
using MissilePredictor.API.History;

namespace MissilePredictor.TestKit.History;

/// <summary>Creates history contexts over the SQLite connection string in <see cref="HistoryOptions"/>, without migrating.</summary>
public sealed class SqliteFileHistoryDb(HistoryOptions options) : IDbContextFactory<HistoryDbContext>
{
    public HistoryDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<HistoryDbContext>().UseSqlite(options.ConnectionString).Options);
}
