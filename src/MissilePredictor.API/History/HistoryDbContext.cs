using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MissilePredictor.API.History;

public class HistoryDbContext(DbContextOptions<HistoryDbContext> options) : DbContext(options)
{
    public DbSet<NeptunAlert> Alerts => Set<NeptunAlert>();
    public DbSet<NeptunTrackPoint> TrackPoints => Set<NeptunTrackPoint>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no timezone-aware type: store UTC and hand back DateTimeKind.Utc.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NeptunAlert>(e =>
        {
            e.ToTable("NeptunAlerts");
            e.HasIndex(a => a.OccurredAt);
            e.HasIndex(a => a.ThreatId);
        });

        modelBuilder.Entity<NeptunTrackPoint>(e =>
        {
            e.ToTable("NeptunTrackPoints");
            e.HasIndex(p => new { p.ThreatId, p.ReceivedAt });
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
