using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Console;
using Microsoft.Extensions.ML;
using MissilePredictor.AI.Models;
using MissilePredictor.AI.Services;
using MissilePredictor.AI.Training;
using MissilePredictor.AI.Config;
using MissilePredictor.Config;
using MissilePredictor.Services;
using MissilePredictor.API.Jobs;
using MissilePredictor.Jobs;
using MissilePredictor.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId());

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Telegram:Mock (see appsettings.Mock.json) runs without a Telegram login or connection.
if (builder.Configuration.GetValue<bool>("Telegram:Mock"))
{
    builder.Services.AddSingleton<ITgScraperService, MockTgScraperService>();
}
else
{
    builder.Services
        .AddOptions<TelegramConfig>()
        .Bind(builder.Configuration.GetSection("Telegram"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddSingleton<ITgScraperService, TgScraperService>();
}
builder.Services.AddHostedService(sp => sp.GetRequiredService<ITgScraperService>());

builder.Services
    .AddOptions<MlConfig>()
    .Bind(builder.Configuration.GetSection("ML"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<GoogleConfig>()
    .Bind(builder.Configuration.GetSection("Google"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var mlConfig = builder.Configuration.GetSection("ML").Get<MlConfig>() ?? new MlConfig();
builder.Services
    .AddPredictionEnginePool<InputRow, SentimentPrediction>()
    .FromFile(modelName: "sentiment", filePath: mlConfig.ModelPath, watchForChanges: true);

builder.Services.AddSingleton<GoogleSheetsClient>();
builder.Services.AddSingleton<SentimentModelPipeline>();

// HomeAssistant:Mock writes alerts to the console instead of calling the webhook.
if (builder.Configuration.GetValue<bool>("HomeAssistant:Mock"))
{
    builder.Services.AddSingleton<IAlertClient, ConsoleAlertClient>();
}
else
{
    builder.Services
        .AddOptions<HomeAssistantConfig>()
        .Bind(builder.Configuration.GetSection("HomeAssistant"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddSingleton<IAlertClient, HomeAssistantClient>();
}

builder.Services
    .AddOptions<NeptunConfig>()
    .Bind(builder.Configuration.GetSection("Neptun"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(new AreaThreatTracker(Area.Lviv));
builder.Services.AddHostedService<NeptunStreamService>();

builder.Services.AddSingleton<PredictionDangerousMessageService>();
builder.Services.AddHangfire(x => x
    .UseInMemoryStorage()
    .UseConsole());
builder.Services.AddHangfireServer();


var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
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

app.Run();

// Lets the integration tests boot the app with WebApplicationFactory<Program>.
public partial class Program { }
