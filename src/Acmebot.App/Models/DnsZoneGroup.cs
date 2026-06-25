using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public class DnsZoneGroup
{
    [JsonPropertyName("dnsProviderName")]
    public required string DnsProviderName { get; set; }

    [JsonPropertyName("dnsZones")]
    public required IReadOnlyList<DnsZoneItem> DnsZones { get; set; }
}
