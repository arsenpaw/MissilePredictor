using System.Text.Encodings.Web;
using System.Text.Json;
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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<TgScraperService>();
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

app.MapGet("/alerts", async (
    PredictionDangerousMessageService svc,
    TgScraperService thSvc,
    ILogger<Program> logger) =>
{
    var messages = (await thSvc.GetAndSaveUnreadMessagesAsync()).ToList();
    
    var predictionResponse = svc.PredictMany(messages.Select(x => x.Text));

    var concat = messages
        .Zip(predictionResponse, (m, p) => new { m, p }).ToList();
    logger.LogWarning("Prediction result: {prediction}",
        JsonSerializer.Serialize(concat, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    var dangerous = concat.Where(x => x.p.Prediction)
        .Select(x => new AlertMessageDto { Message = x.m.Text });

    return Results.Ok(dangerous);
});

app.Run();
