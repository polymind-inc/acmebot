using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Acmebot.App.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Notifications;

public partial class WebhookInvoker(IWebhookPayloadBuilder webhookPayloadBuilder, IHttpClientFactory httpClientFactory, IOptions<WebhookOptions> options, ILogger<WebhookInvoker> logger)
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly WebhookOptions _options = options.Value;

    public Task SendCompletedEventAsync(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        if ((_options.Events & WebhookEvents.Completed) == 0)
        {
            return Task.CompletedTask;
        }

        return SendEventAsync(() => webhookPayloadBuilder.BuildCompleted(certificateName, expirationDate, dnsNames, acmeEndpoint));
    }

    public Task SendFailedEventAsync(string certificateName, IEnumerable<string> dnsNames)
    {
        if ((_options.Events & WebhookEvents.Failed) == 0)
        {
            return Task.CompletedTask;
        }

        return SendEventAsync(() => webhookPayloadBuilder.BuildFailed(certificateName, dnsNames));
    }

    private async Task SendEventAsync(Func<object> payloadFactory)
    {
        if (_options.Endpoint is null)
        {
            return;
        }

        try
        {
            var payload = payloadFactory();
            var httpClient = httpClientFactory.CreateClient();
            using var content = JsonContent.Create(payload, options: s_jsonSerializerOptions);
            await content.LoadIntoBufferAsync();

            using var response = await httpClient.PostAsync(_options.Endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var reason = await response.Content.ReadAsStringAsync();

                LogFailedInvokeWebhook(logger, response.StatusCode, reason);
            }
        }
        catch (Exception ex)
        {
            LogWebhookDeliveryException(logger, ex);
        }
    }

    [LoggerMessage(LogLevel.Warning, "Webhook delivery failed. StatusCode: {ResponseStatusCode}. ResponseBody: {Reason}")]
    private static partial void LogFailedInvokeWebhook(ILogger logger, HttpStatusCode responseStatusCode, string reason);

    [LoggerMessage(LogLevel.Warning, "Webhook delivery threw an exception and was ignored.")]
    private static partial void LogWebhookDeliveryException(ILogger logger, Exception exception);
}
