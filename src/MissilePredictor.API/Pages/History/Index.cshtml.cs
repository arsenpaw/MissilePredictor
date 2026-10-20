using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using MissilePredictor.API.Areas;
using MissilePredictor.API.History;
using MissilePredictor.API.Neptun;

namespace MissilePredictor.API.Pages.History;

public class IndexModel(HistoryQueries queries, IOptions<NeptunConfig> config) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Area { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public AlertPage Alerts { get; private set; } = new([], 0, 1, 50);
    public IReadOnlyList<AreaOptions> Areas { get; private set; } = [];
    public IReadOnlyList<string> Types { get; private set; } = [];

    public bool HasPrevious => Alerts.Page > 1;
    public bool HasNext => Alerts.Page * Alerts.PageSize < Alerts.Total;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Areas = config.Value.Areas;
        Types = await queries.GetThreatTypesAsync(ct);

        // The date inputs pick whole days, so "to" includes the chosen day.
        var filter = new AlertFilter(From, To?.AddDays(1), Area, Type, Math.Max(PageNumber, 1));
        Alerts = await queries.GetAlertsAsync(filter, ct);
    }
}
