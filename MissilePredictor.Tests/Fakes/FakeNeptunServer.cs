using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MissilePredictor.Models;

namespace MissilePredictor.Tests.Fakes;

/// <summary>A local stand-in for wss://neptun.in.ua/api/v1/stream that the test drives frame by frame.</summary>
internal sealed class FakeNeptunServer : IAsyncDisposable
{
    // ~4.5 km north of Lviv centre, and Kyiv (~470 km away).
    public const double InsideLat = 49.88, InsideLon = 24.03;
    public const double KyivLat = 50.4501, KyivLon = 30.5234;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Channel<Connection> _connections = Channel.CreateUnbounded<Connection>();
    private readonly List<Connection> _open = [];
    private WebApplication _app = null!;

    private FakeNeptunServer()
    {
    }

    public string Url { get; private set; } = "";

    public static async Task<FakeNeptunServer> StartAsync()
    {
        var server = new FakeNeptunServer();
        (server._app, var url) = await LocalServer.StartAsync(app =>
        {
            app.UseWebSockets();
            app.Map("/stream", server.AcceptAsync);
        });
        server.Url = url.Replace("http://", "ws://") + "/stream";
        return server;
    }

    public async Task<Connection> NextConnectionAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await _connections.Reader.ReadAsync(cts.Token);
    }

    public static string Snapshot(params NeptunThreat[] threats) =>
        Frame("snapshot", new NeptunSnapshot { Threats = [..threats] });

    public static string Upsert(NeptunThreat threat) => Frame("upsert", threat);

    private static string Frame(string type, object data) =>
        JsonSerializer.Serialize(new { type, ts = DateTimeOffset.UtcNow, data }, JsonOptions);

    /// <summary>An active UAV; <paramref name="explanation"/> ends the alert text, so tests can tell alerts apart.</summary>
    public static NeptunThreat Threat(string id, double lat, double lon, string? explanation = null) => new()
    {
        Id = id,
        Type = "uav",
        Title = "БпЛА",
        Lat = lat,
        Lon = lon,
        Status = "active",
        ExplanationShort = explanation ?? id
    };

    private async Task AcceptAsync(HttpContext context)
    {
        var connection = new Connection(await context.WebSockets.AcceptWebSocketAsync());
        lock (_open) _open.Add(connection);
        _connections.Writer.TryWrite(connection);

        // Keep the request (and so the socket) alive until the test closes it.
        await connection.Closed;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_open) _open.ForEach(c => c.Release());
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    public sealed class Connection(WebSocket socket)
    {
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Closed => _closed.Task;

        public Task SendAsync(string frame) =>
            socket.SendAsync(Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text, true, CancellationToken.None);

        /// <summary>Drops the link the way the real server would, which makes the service reconnect.</summary>
        public async Task CloseAsync()
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "test", CancellationToken.None);
            Release();
        }

        public void Release() => _closed.TrySetResult();
    }
}
