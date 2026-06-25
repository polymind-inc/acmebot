using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public class DnsZoneItem
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
