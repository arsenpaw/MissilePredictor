using Microsoft.EntityFrameworkCore;

namespace MissilePredictor.API.History;

public class HistoryQueries(IDbContextFactory<HistoryDbContext> dbFactory)
{
    private const int MaxPageSize = 200;

    public async Task<AlertPage> GetAlertsAsync(AlertFilter filter, CancellationToken ct)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        IQueryable<NeptunAlert> query = db.Alerts;

        if (filter.From is { } from)
            query = query.Where(a => a.OccurredAt >= AsUtc(from));
        if (filter.To is { } to)
            query = query.Where(a => a.OccurredAt < AsUtc(to));
        if (!string.IsNullOrEmpty(filter.Area))
            query = query.Where(a => a.AreaTitle == filter.Area);
        if (!string.IsNullOrEmpty(filter.Type))
            query = query.Where(a => a.Type == filter.Type);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new AlertPage(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<NeptunTrackPoint>> GetTrackAsync(string threatId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.TrackPoints
            .Where(p => p.ThreatId == threatId)
            .OrderBy(p => p.UpdatedAt).ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetThreatTypesAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Alerts.Select(a => a.Type).Distinct().OrderBy(t => t).ToListAsync(ct);
    }

    // Query-string dates arrive with Kind Unspecified; the history stores UTC.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
