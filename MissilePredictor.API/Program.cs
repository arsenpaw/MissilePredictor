using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.ML;
using MissilePredictor.AI.Models;
using MissilePredictor.AI.Services;
using MissilePredictor.Config;
using MissilePredictor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var modelPath = builder.Configuration["ML:ModelPath"] ?? "models/sentiment.zip";
builder.Services.AddScoped<TgScraperService>();
builder.Services.Configure<TelegramConfig>(builder.Configuration.GetSection("Telegram"));
builder.Services
    .AddPredictionEnginePool<InputRow, SentimentPrediction>()
    .FromFile(modelName: "sentiment", filePath: modelPath, watchForChanges: true);

builder.Services.AddSingleton<PredictionDangerousMessageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/alerts", async (
    PredictionDangerousMessageService svc,
    TgScraperService thSvc,
    ILogger<Program> logger) =>
{
    var messages = await thSvc.GetUnreadMessagesAsync();
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
        .Select(x => new { message = x.m.Text });

    return Results.Ok(dangerous);
});

app.Run();