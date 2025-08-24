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

app.MapPost("/alerts",async  (PredictionDangerousMessageService svc, TgScraperService thSvc) =>
{
    var messages = await thSvc.GetUnreadMessagesAsync();
    var predictionResponse = svc.PredictMany(messages.Select(x => x.Text));
    var dangerousMessage = messages
        .Zip(predictionResponse)
        .Where(x => x.Second.Prediction is true)
        .Select(x => x.First.Text);
    
    return Results.Ok(dangerousMessage);
});

app.Run();