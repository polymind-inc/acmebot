using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public sealed record CertificateIssuanceStatus
{
    [JsonPropertyName("certificateName")]
    public required string CertificateName { get; init; }

    [JsonPropertyName("step")]
    public required string Step { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }
}
