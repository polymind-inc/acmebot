using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acmebot.Acme.Models;

public sealed record AcmeDirectoryResource
{
    [JsonPropertyName("newNonce")]
    public required Uri NewNonce { get; init; }

    [JsonPropertyName("newAccount")]
    public required Uri NewAccount { get; init; }

    [JsonPropertyName("newOrder")]
    public required Uri NewOrder { get; init; }

    [JsonPropertyName("newAuthz")]
    public Uri? NewAuthorization { get; init; }

    [JsonPropertyName("revokeCert")]
    public Uri? RevokeCertificate { get; init; }

    [JsonPropertyName("keyChange")]
    public Uri? KeyChange { get; init; }

    [JsonPropertyName("renewalInfo")]
    public Uri? RenewalInfo { get; init; }

    [JsonPropertyName("meta")]
    public AcmeDirectoryMetadata? Metadata { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed record AcmeDirectoryMetadata
{
    [JsonPropertyName("termsOfService")]
    public Uri? TermsOfService { get; init; }

    [JsonPropertyName("website")]
    public Uri? Website { get; init; }

    [JsonPropertyName("caaIdentities")]
    public IReadOnlyList<string> CaaIdentities { get; init; } = [];

    [JsonPropertyName("externalAccountRequired")]
    public bool? ExternalAccountRequired { get; init; }

    [JsonPropertyName("profiles")]
    public IReadOnlyDictionary<string, string> Profiles { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
