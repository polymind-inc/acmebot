using System.ComponentModel.DataAnnotations;

namespace Acmebot.App.Options;

public class AcmebotOptions
{
    [Required]
    public required Uri Endpoint { get; set; }

    [Required]
    public required string Contacts { get; set; }

    [Required]
    public required string VaultBaseUrl { get; set; }

    public Uri? Webhook { get; set; }

    [Required]
    public string Environment { get; set; } = "AzureCloud";

    public string? PreferredChain { get; set; }

    public string? PreferredProfile { get; set; }

    [Range(0, 365)]
    public int RenewBeforeExpiry { get; set; } = 30;

    public bool UseSystemNameServer { get; set; } = false;

    public string? ManagedIdentityClientId { get; set; }

    public ExternalAccountBindingOptions? ExternalAccountBinding { get; set; }

    // Properties should be in alphabetical order
    public AkamaiEdgeDnsOptions? Akamai { get; set; }

    public AzureDnsOptions? AzureDns { get; set; }

    public AzurePrivateDnsOptions? AzurePrivateDns { get; set; }

    public CloudflareOptions? Cloudflare { get; set; }

    public CustomDnsOptions? CustomDns { get; set; }

    public DnsMadeEasyOptions? DnsMadeEasy { get; set; }

    public GandiLiveDnsOptions? GandiLiveDns { get; set; }

    public GoDaddyOptions? GoDaddy { get; set; }

    public GoogleDnsOptions? GoogleDns { get; set; }

    public IonosDnsOptions? IonosDns { get; set; }

    public Route53Options? Route53 { get; set; }

    public TransIpOptions? TransIp { get; set; }
}
