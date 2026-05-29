namespace Acmebot.App.Options;

public class AzurePrivateDnsOptions
{
    public string? ManagedIdentityClientId { get; set; }

    public required string SubscriptionId { get; set; }
}
