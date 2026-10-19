using Microsoft.Extensions.ML;
using MissilePredictor.AI.Sentiment;
using MissilePredictor.AI.Sentiment.Models;
using MissilePredictor.AI.Sentiment.Training;

namespace MissilePredictor.API.Extensions;

public static class SentimentServiceCollectionExtensions
{
    public static IServiceCollection AddSentimentModel(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<MlConfig>()
            .Bind(configuration.GetSection("ML"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var mlConfig = configuration.GetSection("ML").Get<MlConfig>() ?? new MlConfig();
        services
            .AddPredictionEnginePool<InputRow, SentimentPrediction>()
            .FromFile(modelName: "sentiment", filePath: mlConfig.ModelPath, watchForChanges: true);

        services.AddSingleton<SentimentModelPipeline>();
        services.AddSingleton<PredictionDangerousMessageService>();

        return services;
    }
}
