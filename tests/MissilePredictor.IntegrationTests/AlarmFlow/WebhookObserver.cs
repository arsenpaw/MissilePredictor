using System.Diagnostics;
using System.Threading.Channels;

namespace MissilePredictor.IntegrationTests.AlarmFlow;

/// <summary>
/// Watches the webhook call from outside the app through the diagnostic events every HttpClient
/// raises, so the app's own client and HttpClient send it untouched.
/// </summary>
internal sealed class WebhookObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly Channel<WebhookCall> _calls = Channel.CreateUnbounded<WebhookCall>();
    private readonly List<IDisposable> _subscriptions = [];

    public static WebhookObserver Subscribe()
    {
        var observer = new WebhookObserver();
        observer._subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(observer));
        return observer;
    }

    public async Task<WebhookCall> NextCallAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await _calls.Reader.ReadAsync(cts.Token);
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "HttpHandlerDiagnosticListener")
            lock (_subscriptions) _subscriptions.Add(listener.Subscribe(this));
    }

    public void OnNext(KeyValuePair<string, object?> e)
    {
        if (e.Key != "System.Net.Http.HttpRequestOut.Stop" || e.Value is null) return;

        var request = Property<HttpRequestMessage>(e.Value, "Request");
        if (request?.RequestUri?.AbsolutePath.Contains("/api/webhook/") != true) return;
        var response = Property<HttpResponseMessage>(e.Value, "Response");

        // Raised before PostAsync returns, while both contents are still buffered and readable.
        _calls.Writer.TryWrite(new WebhookCall(
            request.RequestUri,
            request.Headers.Authorization?.ToString() ?? "",
            request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "",
            response?.StatusCode,
            response?.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? ""));
    }

    private static T? Property<T>(object payload, string name) where T : class =>
        payload.GetType().GetProperty(name)?.GetValue(payload) as T;

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        lock (_subscriptions) _subscriptions.ForEach(s => s.Dispose());
    }
}
