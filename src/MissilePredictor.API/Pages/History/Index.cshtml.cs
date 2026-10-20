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
    public bool IsFiltered => From is not null || To is not null || !string.IsNullOrEmpty(Area) || !string.IsNullOrEmpty(Type);

    /// <summary>This page's alerts grouped by Kyiv calendar day, newest first.</summary>
    public IEnumerable<IGrouping<DateTime, NeptunAlert>> Days =>
        Alerts.Items.GroupBy(a => KyivTime.FromUtc(a.OccurredAt).Date);

    /// <summary>Query values that keep the current filters on another page of results.</summary>
    public Dictionary<string, string?> RouteFor(int page) => new()
    {
        ["From"] = From?.ToString("yyyy-MM-dd"),
        ["To"] = To?.ToString("yyyy-MM-dd"),
        ["Area"] = Area,
        ["Type"] = Type,
        ["PageNumber"] = page.ToString()
    };

    /// <summary>Ukrainian plural of "тривога" for <paramref name="count"/>.</summary>
    public static string AlertsWord(int count) => (count % 100, count % 10) switch
    {
        ( >= 11 and <= 14, _) => "тривог",
        (_, 1) => "тривога",
        (_, >= 2 and <= 4) => "тривоги",
        _ => "тривог"
    };

    public async Task OnGetAsync(CancellationToken ct)
    {
        Areas = config.Value.Areas;
        Types = await queries.GetThreatTypesAsync(ct);

        // The date inputs pick whole Kyiv days, so "to" includes the chosen day.
        var from = From is { } f ? KyivTime.DayStartUtc(f) : (DateTime?)null;
        var to = To is { } t ? KyivTime.DayStartUtc(t.AddDays(1)) : (DateTime?)null;
        var filter = new AlertFilter(from, to, Area, Type, Math.Max(PageNumber, 1));
        Alerts = await queries.GetAlertsAsync(filter, ct);
    }
}
