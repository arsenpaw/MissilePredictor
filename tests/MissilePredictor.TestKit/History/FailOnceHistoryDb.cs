using Microsoft.EntityFrameworkCore;
using MissilePredictor.API.History;

namespace MissilePredictor.TestKit.History;

/// <summary>Wraps a history factory so the first <see cref="CreateDbContextAsync"/> call throws.</summary>
public sealed class FailOnceHistoryDb(IDbContextFactory<HistoryDbContext> inner) : IDbContextFactory<HistoryDbContext>
{
    private readonly TaskCompletionSource _firstFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    /// <summary>Completes once the failing first call has happened.</summary>
    public Task FirstFailure => _firstFailure.Task;

    public HistoryDbContext CreateDbContext() => inner.CreateDbContext();

    public Task<HistoryDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Increment(ref _calls) == 1
            ? Fail()
            : Task.FromResult(inner.CreateDbContext());

    private Task<HistoryDbContext> Fail()
    {
        _firstFailure.SetResult();
        throw new InvalidOperationException("disk full");
    }
}
