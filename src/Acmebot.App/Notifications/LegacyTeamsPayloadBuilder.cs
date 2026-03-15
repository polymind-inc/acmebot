namespace Acmebot.App.Notifications;

internal class LegacyTeamsPayloadBuilder : IWebhookPayloadBuilder
{
    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            title = "Acmebot",
            text = @$"A new certificate has been issued.

**Certificate Name**: {certificateName}

**Expiration Date**: {expirationDate}

**ACME Endpoint**: {acmeEndpoint}

**DNS Names**: {string.Join(", ", dnsNames)}",
            themeColor = "2EB886"
        };
    }

    public object BuildFailed(string certificateName, IEnumerable<string> dnsNames)
    {
        return new
        {
            title = "Acmebot",
            text = @$"Failed to issue a new certificate.

**Certificate Name**: {certificateName}

**DNS Names**: {string.Join(", ", dnsNames)}",
            themeColor = "A30200"
        };
    }
}
