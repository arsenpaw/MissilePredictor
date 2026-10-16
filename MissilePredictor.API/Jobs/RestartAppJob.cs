using Hangfire;
using Hangfire.Console;
using Hangfire.Server;

namespace MissilePredictor.API.Jobs;

// Exits the process so Docker's restart policy brings up a fresh instance.
public class RestartAppJob
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxWait = TimeSpan.FromHours(2);

    private readonly IHostApplicationLifetime _lifetime;
    private readonly JobStorage _jobStorage;
    private readonly ILogger<RestartAppJob> _logger;

    public RestartAppJob(IHostApplicationLifetime lifetime, JobStorage jobStorage, ILogger<RestartAppJob> logger)
    {
        _lifetime = lifetime;
        _jobStorage = jobStorage;
        _logger = logger;
    }

    public async Task Execute(PerformContext context)
    {
        var ownJobId = context.BackgroundJob.Id;
        var deadline = DateTime.UtcNow + MaxWait;
        var monitoring = _jobStorage.GetMonitoringApi();

        while (true)
        {
            var otherRunning = monitoring.ProcessingJobs(0, int.MaxValue)
                .Count(j => j.Key != ownJobId);

            if (otherRunning == 0)
                break;

            if (DateTime.UtcNow >= deadline)
            {
                _logger.LogWarning("{Count} job(s) still running after {MaxWait}, restarting anyway", otherRunning, MaxWait);
                context.WriteLine($"{otherRunning} job(s) still running after {MaxWait}, restarting anyway.");
                break;
            }

            context.WriteLine($"Waiting for {otherRunning} running job(s) to finish...");
            await Task.Delay(PollInterval, context.CancellationToken.ShutdownToken);
        }

        _logger.LogInformation("Daily restart: stopping application");
        context.WriteLine("Stopping application, Docker will restart it.");
        _lifetime.StopApplication();
    }
}
