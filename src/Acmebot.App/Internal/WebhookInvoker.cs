using Acmebot.App.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Internal;

public class WebhookInvoker(IWebhookPayloadBuilder webhookPayloadBuilder, IHttpClientFactory httpClientFactory, IOptions<AcmebotOptions> options, ILogger<WebhookInvoker> logger)
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

        var response = await httpClient.PostAsync(_options.Webhook, payload);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed invoke webhook. Status Code = {ResponseStatusCode}, Reason = {ReadAsStringAsync}", response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
