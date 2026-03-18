using System.Net;
using System.Net.Http.Json;

using Acmebot.App.Extensions;
using Acmebot.App.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Notifications;

public partial class WebhookInvoker(IWebhookPayloadBuilder webhookPayloadBuilder, IHttpClientFactory httpClientFactory, IOptions<AcmebotOptions> options, ILogger<WebhookInvoker> logger)
{
    private readonly AcmebotOptions _options = options.Value;

    public Task SendCompletedEventAsync(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        var payload = webhookPayloadBuilder.BuildCompleted(certificateName, expirationDate, dnsNames, acmeEndpoint);

        return SendEventAsync(payload);
    }

    public Task SendFailedEventAsync(string certificateName, IEnumerable<string> dnsNames)
    {
        var payload = webhookPayloadBuilder.BuildFailed(certificateName, dnsNames);

        return SendEventAsync(payload);
    }

    private async Task SendEventAsync(object payload)
    {
        if (_options.Webhook is null)
        {
            return;
        }

        var httpClient = httpClientFactory.CreateClient();

        var response = await httpClient.PostAsJsonAsync(_options.Webhook, payload);

        if (!response.IsSuccessStatusCode)
        {
            var reason = await response.Content.ReadAsStringAsync();

            LogFailedInvokeWebhook(logger, response.StatusCode, reason);
        }
    }

    [LoggerMessage(LogLevel.Warning, "Webhook delivery failed. StatusCode: {ResponseStatusCode}. ResponseBody: {Reason}")]
    private static partial void LogFailedInvokeWebhook(ILogger logger, HttpStatusCode responseStatusCode, string reason);
}
