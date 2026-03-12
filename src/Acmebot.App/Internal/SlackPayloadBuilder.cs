namespace Acmebot.App.Internal;

internal class SlackPayloadBuilder : IWebhookPayloadBuilder
{
    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            username = "Acmebot",
            attachments = new[]
            {
                new
                {
                    text = "A new certificate has been issued.",
                    color = "good",
                    fields = new object[]
                    {
                        new
                        {
                            title = "Certificate Name",
                            value= certificateName,
                            @short = true
                        },
                        new
                        {
                            title = "Expiration Date",
                            value = expirationDate,
                            @short = true
                        },
                        new
                        {
                            title = "ACME Endpoint",
                            value = acmeEndpoint,
                            @short = true
                        },
                        new
                        {
                            title = "DNS Names",
                            value = string.Join("\n", dnsNames)
                        }
                    }
                }
            }
        };
    }

    public object BuildFailed(string certificateName, IEnumerable<string> dnsNames)
    {
        return new
        {
            username = "Acmebot",
            attachments = new[]
            {
                new
                {
                    text = "Failed to issue a new certificate.",
                    color = "danger",
                    fields = new object[]
                    {
                        new
                        {
                            title = "Certificate Name",
                            value= certificateName,
                            @short = true
                        },
                        new
                        {
                            title = "DNS Names",
                            value = string.Join("\n", dnsNames)
                        }
                    }
                }
            }
        };
    }
}
