namespace Acmebot.App.Notifications;

public interface IWebhookPayloadBuilder
{
    object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint);
    object BuildFailed(string certificateName, IEnumerable<string> dnsNames);
}
