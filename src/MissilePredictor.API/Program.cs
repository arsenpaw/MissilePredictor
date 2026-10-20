using MissilePredictor.API.Extensions;
using MissilePredictor.API.Telegram;
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

// The Telegram:Mock / HomeAssistant:Mock switches are read from builder.Configuration here, at
// registration time, so the integration tests can flip them with UseSetting.
var telegramAi = TelegramAiSwitch.IsEnabled(builder.Configuration);
if (telegramAi)
{
    builder.Services
        .AddTelegramScraper(builder.Configuration)
        .AddSentimentModel(builder.Configuration)
        .AddGoogleSheets(builder.Configuration);
}

builder.Services
    .AddAlerts(builder.Configuration)
    .AddNeptunStream(builder.Configuration)
    .AddJobs();


var app = builder.Build();

if (!telegramAi)
{
    app.Logger.LogWarning(
        "Telegram/AI pipeline disabled ({Key}=false): no Telegram scraper, ML model, Google Sheets or nightly jobs.",
        TelegramAiSwitch.Key);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseJobs(telegramAi);

app.Run();

// Lets the integration tests boot the app with WebApplicationFactory<Program>.
public partial class Program { }
