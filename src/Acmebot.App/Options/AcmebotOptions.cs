using System.ComponentModel.DataAnnotations;

namespace Acmebot.App.Options;

public class AcmebotOptions
{
    public string Environment { get; set; } = "AzureCloud";

    public string? ManagedIdentityClientId { get; set; }

    [Range(0, 100)]
    public int RenewBeforeExpiry { get; set; } = 30;

    public bool RequireAppRoles { get; set; } = false;

    public bool UseSystemNameServer { get; set; } = false;

    [Required]
    public required string VaultBaseUrl { get; set; }

    // ACME CA settings
    [Required]
    public required string Contacts { get; set; }

    [Required]
    public required Uri Endpoint { get; set; }

    public ExternalAccountBindingOptions? ExternalAccountBinding { get; set; }

    public string? PreferredChain { get; set; }

    public string? PreferredProfile { get; set; }

    // Properties should be in alphabetical order
    public AkamaiEdgeDnsOptions? Akamai { get; set; }

    public AzureDnsOptions? AzureDns { get; set; }

    public AzurePrivateDnsOptions? AzurePrivateDns { get; set; }

    public CloudflareOptions? Cloudflare { get; set; }

    public CustomDnsOptions? CustomDns { get; set; }

    public DnsMadeEasyOptions? DnsMadeEasy { get; set; }

    public EasyDnsOptions? EasyDns { get; set; }

    public GandiLiveDnsOptions? GandiLiveDns { get; set; }

    public GoDaddyOptions? GoDaddy { get; set; }

    public GoogleDnsOptions? GoogleDns { get; set; }

    public IonosDnsOptions? IonosDns { get; set; }

    public OvhOptions? Ovh { get; set; }

    public PowerDnsOptions? PowerDns { get; set; }

    public RegfishOptions? Regfish { get; set; }

    public Route53Options? Route53 { get; set; }

    public TransIpOptions? TransIp { get; set; }

    public UnitedDomainsOptions? UnitedDomains { get; set; }
}
