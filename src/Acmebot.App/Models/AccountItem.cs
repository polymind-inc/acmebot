using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public class AccountItem
{
    [JsonPropertyName("directoryUrl")]
    public required Uri DirectoryUrl { get; set; }

    [JsonPropertyName("caaIdentities")]
    public required IReadOnlyList<string> CaaIdentities { get; set; }

    [JsonPropertyName("dnsPersist01")]
    public required DnsPersist01MetadataItem DnsPersist01 { get; set; }
}

public class DnsPersist01MetadataItem
{
    [JsonPropertyName("preProvisioningMetadataAvailable")]
    public required bool PreProvisioningMetadataAvailable { get; set; }

    [JsonPropertyName("issuerDomainNames")]
    public required IReadOnlyList<string> IssuerDomainNames { get; set; }

    [JsonPropertyName("accountHashPrefix")]
    public string? AccountHashPrefix { get; set; }
}
