using Microsoft.EntityFrameworkCore;

namespace MissilePredictor.API.History;

/// <summary>Persists queued history entities in batches. A failed batch is logged and dropped; the writer never stops.</summary>
public sealed class HistoryWriterService(
    NeptunHistoryRecorder recorder,
    IDbContextFactory<HistoryDbContext> dbFactory,
    ILogger<HistoryWriterService> logger) : BackgroundService
{
    private const int MaxBatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            while (await recorder.Pending.WaitToReadAsync(ct))
                await WriteBatchAsync();
        }
        catch (OperationCanceledException)
        {
            // Stopping: fall through to flush what is left.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "History writer loop failed");
        }

        try
        {
            while (recorder.Pending.TryPeek(out _))
                await WriteBatchAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "History writer failed to flush on stop");
        }
    }

    private async Task WriteBatchAsync()
    {
        var batch = new List<object>();
        while (batch.Count < MaxBatchSize && recorder.Pending.TryRead(out var item))
            batch.Add(item);
        if (batch.Count == 0) return;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.AddRange(batch);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save {Count} history item(s); batch dropped", batch.Count);
        }
    }
}
