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

builder.Services.AddSingleton<TgScraperService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TgScraperService>());
builder.Services
    .AddOptions<TelegramConfig>()
    .Bind(builder.Configuration.GetSection("Telegram"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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

builder.Services
    .AddOptions<HomeAssistantConfig>()
    .Bind(builder.Configuration.GetSection("HomeAssistant"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<HomeAssistantClient>();

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

app.Run();
