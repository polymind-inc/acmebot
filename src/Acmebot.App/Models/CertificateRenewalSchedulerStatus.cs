using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public sealed record CertificateRenewalSchedulerStatus
{
    [JsonPropertyName("certificateName")]
    public required string CertificateName { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("nextCheck")]
    public DateTimeOffset? NextCheck { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }
}
