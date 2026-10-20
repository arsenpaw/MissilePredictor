using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using MissilePredictor.API.History;

namespace MissilePredictor.API.Extensions;

public static class HistoryServiceCollectionExtensions
{
    public static IServiceCollection AddHistory(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<HistoryOptions>()
            .Bind(configuration.GetSection("History"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContextFactory<HistoryDbContext>((sp, builder) =>
            builder.UseSqlite(sp.GetRequiredService<IOptions<HistoryOptions>>().Value.ConnectionString));
        services.AddHostedService<HistoryMigrationService>();

        services.AddSingleton<HistoryQueries>();
        services.AddSingleton<NeptunHistoryRecorder>();
        services.AddSingleton<INeptunHistoryRecorder>(sp => sp.GetRequiredService<NeptunHistoryRecorder>());
        services.AddHostedService<HistoryWriterService>();

        // Keep Ukrainian text readable in rendered pages instead of &#x...; escapes.
        services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
        services.AddRazorPages();

        return services;
    }
}
