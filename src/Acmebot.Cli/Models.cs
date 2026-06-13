using System.Text.Json.Serialization;

namespace Acmebot.Cli;

internal sealed class CertificatePolicyItem
{
    [JsonPropertyName("certificateName")]
    public string? CertificateName { get; set; }

    [JsonPropertyName("dnsNames")]
    public required string[] DnsNames { get; set; }

    [JsonPropertyName("dnsProviderName")]
    public string? DnsProviderName { get; set; }

    [JsonPropertyName("keyType")]
    public required string KeyType { get; set; }

    [JsonPropertyName("keySize")]
    public int? KeySize { get; set; }

    [JsonPropertyName("keyCurveName")]
    public string? KeyCurveName { get; set; }

    [JsonPropertyName("reuseKey")]
    public bool? ReuseKey { get; set; }

    [JsonPropertyName("dnsAlias")]
    public string? DnsAlias { get; set; }

    [JsonPropertyName("tags")]
    public IDictionary<string, string>? Tags { get; set; }
}

internal sealed class CertificateItem
{
    [JsonPropertyName("id")]
    public required Uri Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("dnsNames")]
    public required IReadOnlyList<string> DnsNames { get; set; }

    [JsonPropertyName("dnsProviderName")]
    public string? DnsProviderName { get; set; }

    [JsonPropertyName("createdOn")]
    public DateTimeOffset CreatedOn { get; set; }

    [JsonPropertyName("expiresOn")]
    public DateTimeOffset ExpiresOn { get; set; }

    [JsonPropertyName("x509Thumbprint")]
    public string? X509Thumbprint { get; set; }

    [JsonPropertyName("keyType")]
    public string? KeyType { get; set; }

    [JsonPropertyName("keySize")]
    public int? KeySize { get; set; }

    [JsonPropertyName("keyCurveName")]
    public string? KeyCurveName { get; set; }

    [JsonPropertyName("reuseKey")]
    public bool? ReuseKey { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("isIssuedByAcmebot")]
    public bool IsIssuedByAcmebot { get; set; }

    [JsonPropertyName("isSameEndpoint")]
    public bool IsSameEndpoint { get; set; }

    [JsonPropertyName("acmeEndpoint")]
    public string? AcmeEndpoint { get; set; }

    [JsonPropertyName("dnsAlias")]
    public string? DnsAlias { get; set; }

    [JsonPropertyName("tags")]
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
}

internal sealed class DnsZoneItem
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

internal sealed class DnsZoneGroup
{
    [JsonPropertyName("dnsProviderName")]
    public required string DnsProviderName { get; set; }

    [JsonPropertyName("dnsZones")]
    public IReadOnlyList<DnsZoneItem>? DnsZones { get; set; }
}

internal sealed class ProblemDetails
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }
}

internal sealed record OperationResult(string Status, Uri OperationLocation);

internal sealed record CertificateCommandResult(string Status, string CertificateName);
