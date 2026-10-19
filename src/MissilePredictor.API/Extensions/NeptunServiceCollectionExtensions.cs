using MissilePredictor.API.Areas;
using MissilePredictor.API.Neptun;

namespace MissilePredictor.API.Extensions;

public static class NeptunServiceCollectionExtensions
{
    public static IServiceCollection AddNeptunStream(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<NeptunConfig>()
            .Bind(configuration.GetSection("Neptun"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(new AreaThreatTracker(Area.Lviv));
        services.AddHostedService<NeptunStreamService>();

        return services;
    }
}
