using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

        return services;
    }
}
