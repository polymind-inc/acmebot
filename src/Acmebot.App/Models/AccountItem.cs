using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public class AccountItem
{
    [JsonPropertyName("accountUri")]
    public required Uri AccountUri { get; set; }

    [JsonPropertyName("directoryUrl")]
    public required Uri DirectoryUrl { get; set; }

    [JsonPropertyName("caaIdentities")]
    public required IReadOnlyList<string> CaaIdentities { get; set; }
}
