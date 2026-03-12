using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeRenewalInfoResource
{
    [JsonPropertyName("suggestedWindow")]
    public required AcmeRenewalWindow SuggestedWindow { get; init; }

    [JsonPropertyName("explanationURL")]
    public Uri? ExplanationUrl { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed record AcmeRenewalWindow
{
    [JsonPropertyName("start")]
    public required DateTimeOffset Start { get; init; }

    [JsonPropertyName("end")]
    public required DateTimeOffset End { get; init; }
}
