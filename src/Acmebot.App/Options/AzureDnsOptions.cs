namespace Acmebot.App.Options;

public class AzureDnsOptions
{
    public string? ManagedIdentityClientId { get; set; }

    public required string SubscriptionId { get; set; }
}
