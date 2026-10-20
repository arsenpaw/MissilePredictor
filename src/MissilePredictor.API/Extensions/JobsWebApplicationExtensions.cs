using Hangfire;
using Hangfire.Dashboard;
using MissilePredictor.API.Jobs;

namespace MissilePredictor.API.Extensions;

public static class JobsWebApplicationExtensions
{
    /// <summary>Mounts the Hangfire dashboard and, when the Telegram/AI pipeline is enabled, schedules the recurring jobs.</summary>
    public static WebApplication UseJobs(this WebApplication app, bool telegramAiEnabled)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = Array.Empty<IDashboardAuthorizationFilter>()
        });

        if (!telegramAiEnabled)
        {
            // Hangfire storage is in-memory today, but drop stale entries in case it is ever made persistent.
            foreach (var id in new[] { "sync-telegram", "model-training", "restart-app" })
                RecurringJob.RemoveIfExists(id);

            return app;
        }

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
