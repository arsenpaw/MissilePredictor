using Hangfire;
using Hangfire.Dashboard;
using MissilePredictor.API.Jobs;

namespace MissilePredictor.API.Extensions;

public static class JobsWebApplicationExtensions
{
    /// <summary>Mounts the Hangfire dashboard and schedules the recurring jobs.</summary>
    public static WebApplication UseJobs(this WebApplication app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = Array.Empty<IDashboardAuthorizationFilter>()
        });

        RecurringJob.AddOrUpdate<SyncTelegramDatasToSheetsJob>(
            "sync-telegram",
            job => job.Execute(null!),
            Cron.Daily(23));

        RecurringJob.AddOrUpdate<ModelTrainingJob>(
            "model-training",
            job => job.Execute(null!),
            Cron.Daily(0));

        // Runs after the nightly sync and training; waits for any still-running job before exiting.
        RecurringJob.AddOrUpdate<RestartAppJob>(
            "restart-app",
            job => job.Execute(null!),
            Cron.Daily(1));

        return app;
    }
}
