using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MissilePredictor.API.History;

/// <summary>Lets <c>dotnet ef</c> create migrations without booting the app.</summary>
public class HistoryDbContextDesignTimeFactory : IDesignTimeDbContextFactory<HistoryDbContext>
{
    public HistoryDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<HistoryDbContext>()
            .UseSqlite(new HistoryOptions().ConnectionString)
            .Options);
}
