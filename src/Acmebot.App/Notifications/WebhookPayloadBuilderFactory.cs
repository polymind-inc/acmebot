using Acmebot.App.Options;

namespace Acmebot.App.Notifications;

internal static class WebhookPayloadBuilderFactory
{
    public static IWebhookPayloadBuilder Create(AcmebotOptions acmebotOptions, WebhookOptions webhookOptions)
    {
        var payloadType = webhookOptions.PayloadType;

        if (payloadType == WebhookPayloadType.Auto)
        {
            payloadType = DetectPayloadType(webhookOptions.Endpoint);
        }

        return payloadType switch
        {
            WebhookPayloadType.Slack => new SlackPayloadBuilder(),
            WebhookPayloadType.Teams => new TeamsPayloadBuilder(),
            _ => new GenericPayloadBuilder(acmebotOptions)
        };
    }

    private static WebhookPayloadType DetectPayloadType(Uri? endpoint)
    {
        if (endpoint is null)
        {
            return WebhookPayloadType.Generic;
        }

        var host = endpoint.Host;

        if (host.EndsWith("hooks.slack.com", StringComparison.OrdinalIgnoreCase))
        {
            return WebhookPayloadType.Slack;
        }

        if (host.EndsWith(".logic.azure.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".environment.api.powerplatform.com", StringComparison.OrdinalIgnoreCase))
        {
            return WebhookPayloadType.Teams;
        }

        return WebhookPayloadType.Generic;
    }
}
