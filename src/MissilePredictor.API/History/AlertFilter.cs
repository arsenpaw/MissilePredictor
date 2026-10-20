namespace MissilePredictor.API.History;

/// <summary>Filters and paging for the alert history; <c>From</c> is inclusive, <c>To</c> exclusive (UTC).</summary>
public record AlertFilter(
    DateTime? From = null,
    DateTime? To = null,
    string? Area = null,
    string? Type = null,
    int Page = 1,
    int PageSize = 50);
