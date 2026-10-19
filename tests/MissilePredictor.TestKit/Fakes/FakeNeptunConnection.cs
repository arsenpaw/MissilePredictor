using System.Net.WebSockets;
using System.Text;

namespace MissilePredictor.TestKit.Fakes;

/// <summary>One WebSocket accepted by <see cref="FakeNeptunServer"/>, which the test sends frames through.</summary>
public sealed class FakeNeptunConnection(WebSocket socket)
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
