using System.ComponentModel.DataAnnotations;

namespace MissilePredictor.API.History;

public class HistoryOptions
{
    [Required]
    public string ConnectionString { get; set; } = "Data Source=data/history.db";

    /// <summary>How long track points of threats that never triggered an alert are kept.</summary>
    public TimeSpan UnalertedTrackRetention { get; set; } = TimeSpan.FromHours(6);
}
