using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public class CertificateItem
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
