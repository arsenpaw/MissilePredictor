using MissilePredictor.API.History;

namespace MissilePredictor.API.Extensions;

public static class HistoryWebApplicationExtensions
{
    public static WebApplication MapHistory(this WebApplication app)
    {
        app.MapHistoryApi();
        return app;
    }
}
