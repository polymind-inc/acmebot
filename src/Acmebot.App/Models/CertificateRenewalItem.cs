using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public sealed record CertificateRenewalItem
{
    [JsonPropertyName("certificateName")]
    public required string CertificateName { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("statusKind")]
    public required string StatusKind { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("nextCheck")]
    public DateTimeOffset? NextCheck { get; init; }

    [JsonPropertyName("lastCheckedAt")]
    public DateTimeOffset? LastCheckedAt { get; init; }
}
