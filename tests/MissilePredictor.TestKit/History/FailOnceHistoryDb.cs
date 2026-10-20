using Microsoft.EntityFrameworkCore;
using MissilePredictor.API.History;

namespace MissilePredictor.TestKit.History;

/// <summary>Wraps a history factory so the first <see cref="CreateDbContextAsync"/> call throws.</summary>
public sealed class FailOnceHistoryDb(IDbContextFactory<HistoryDbContext> inner) : IDbContextFactory<HistoryDbContext>
{
    private int _calls;

    public HistoryDbContext CreateDbContext() => inner.CreateDbContext();

    public Task<HistoryDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Increment(ref _calls) == 1
            ? throw new InvalidOperationException("disk full")
            : Task.FromResult(inner.CreateDbContext());
}
