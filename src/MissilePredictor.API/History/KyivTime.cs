namespace MissilePredictor.API.History;

/// <summary>Calendar days in Europe/Kyiv, the zone the /history page filters by.</summary>
public static class KyivTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    /// <summary>The UTC instant at which the Kyiv calendar day containing <paramref name="day"/>'s date starts.</summary>
    public static DateTime DayStartUtc(DateTime day) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.Date, DateTimeKind.Unspecified), Zone);

    /// <summary>Kyiv wall-clock time of a stored UTC instant.</summary>
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
