namespace Acmebot.App.Notifications;

internal class TeamsPayloadBuilder : IWebhookPayloadBuilder
{
    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "A new certificate has been issued.",
                                wrap = true
                            },
                            new
                            {
                                type = "FactSet",
                                facts = new object[]
                                {
                                    new
                                    {
                                        title = "Certificate Name",
                                        value = certificateName
                                    },
                                    new
                                    {
                                        title = "Expiration Date",
                                        value = expirationDate
                                    },
                                    new
                                    {
                                        title = "ACME Endpoint",
                                        value = acmeEndpoint
                                    },
                                    new
                                    {
                                        title = "DNS Names",
                                        value = string.Join("\n", dnsNames)
                                    }
                                }
                            }
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
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "Failed to issue a new certificate.",
                                wrap = true
                            },
                            new
                            {
                                type = "FactSet",
                                facts = new object[]
                                {
                                    new
                                    {
                                        title = "Certificate Name",
                                        value = certificateName
                                    },
                                    new
                                    {
                                        title = "DNS Names",
                                        value = string.Join("\n", dnsNames)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
