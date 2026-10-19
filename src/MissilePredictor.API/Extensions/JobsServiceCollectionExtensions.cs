using Hangfire;
using Hangfire.Console;

namespace MissilePredictor.API.Extensions;

public static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddHangfire(x => x
            .UseInMemoryStorage()
            .UseConsole());
        services.AddHangfireServer();

        return services;
    }
}
