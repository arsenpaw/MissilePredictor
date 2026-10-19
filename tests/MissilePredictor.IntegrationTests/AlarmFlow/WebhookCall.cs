using System.Net;

namespace MissilePredictor.IntegrationTests.AlarmFlow;

/// <summary><see cref="Status"/> is null when the request never got a response (DNS, TLS, refused...).</summary>
internal sealed record WebhookCall(Uri? RequestUri, string Authorization, string Body, HttpStatusCode? Status, string ResponseBody);
