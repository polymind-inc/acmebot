using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeSignedMessage
{
    [JsonPropertyName("protected")]
    public required string Protected { get; init; }

    [JsonPropertyName("payload")]
    public required string Payload { get; init; }

    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}
