using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MissilePredictor.API.Neptun;

namespace MissilePredictor.API.History;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/history");

        api.MapGet("/alerts", async ([AsParameters] AlertFilter filter, HistoryQueries queries, CancellationToken ct) =>
            await queries.GetAlertsAsync(filter, ct));

        api.MapGet("/threats/{threatId}/track", async (string threatId, HistoryQueries queries, CancellationToken ct) =>
            await queries.GetTrackAsync(threatId, ct));

        api.MapGet("/areas", (IOptions<NeptunConfig> config) => config.Value.Areas);

        return endpoints;
    }
}
