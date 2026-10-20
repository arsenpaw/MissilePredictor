using Microsoft.Extensions.Options;
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
        services.AddSingleton<IValidateOptions<NeptunConfig>, NeptunConfigValidator>();

        services.AddSingleton<IReadOnlyList<AreaThreatTracker>>(sp => sp
            .GetRequiredService<IOptions<NeptunConfig>>().Value.Areas
            .Select(a => new AreaThreatTracker(a.ToArea()))
            .ToList());
        services.AddHostedService<NeptunStreamService>();

        return services;
    }
}
