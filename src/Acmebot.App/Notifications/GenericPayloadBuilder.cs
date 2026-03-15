using Acmebot.App.Infrastructure;
using Acmebot.App.Options;

namespace Acmebot.App.Notifications;

internal class GenericPayloadBuilder(AcmebotOptions options) : IWebhookPayloadBuilder
{
    public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
    {
        return new
        {
            certificateName,
            expirationDate,
            dnsNames,
            acmeEndpoint,
            keyVaultName = new Uri(options.VaultBaseUrl).Host,
            functionAppName = Constants.FunctionAppName
        };
    }

    public object BuildFailed(string certificateName, IEnumerable<string> dnsNames)
    {
        return new
        {
            certificateName,
            dnsNames,
            keyVaultName = new Uri(options.VaultBaseUrl).Host,
            functionAppName = Constants.FunctionAppName
        };
    }
}
