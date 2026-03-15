using Acmebot.App.Notifications;
using Acmebot.App.Options;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Orchestration;

public class WebhookActivities(
    WebhookInvoker webhookInvoker,
    IOptions<AcmebotOptions> options)
{
    private readonly AcmebotOptions _options = options.Value;

    [Function(nameof(SendCompletedEvent))]
    public Task SendCompletedEvent([ActivityTrigger] (string, DateTimeOffset?, IReadOnlyList<string>) input)
    {
        var (certificateName, expirationDate, dnsNames) = input;

        return webhookInvoker.SendCompletedEventAsync(certificateName, expirationDate, dnsNames, _options.Endpoint.Host);
    }

    [Function(nameof(SendFailedEvent))]
    public Task SendFailedEvent([ActivityTrigger] (string, IReadOnlyList<string>) input)
    {
        var (certificateName, dnsNames) = input;

        return webhookInvoker.SendFailedEventAsync(certificateName, dnsNames);
    }
}
