using Newtonsoft.Json;

namespace Acmebot.App.Models;

public class CertificateItem
{
    [JsonProperty("id")]
    public required Uri Id { get; set; }

    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("dnsNames")]
    public required IReadOnlyList<string> DnsNames { get; set; }

    [JsonProperty("dnsProviderName")]
    public string? DnsProviderName { get; set; }

    [JsonProperty("createdOn")]
    public DateTimeOffset CreatedOn { get; set; }

    [JsonProperty("expiresOn")]
    public DateTimeOffset ExpiresOn { get; set; }

    [JsonProperty("x509Thumbprint")]
    public string? X509Thumbprint { get; set; }

    [JsonProperty("keyType")]
    public string? KeyType { get; set; }

    [JsonProperty("keySize")]
    public int? KeySize { get; set; }

    [JsonProperty("keyCurveName")]
    public string? KeyCurveName { get; set; }

    [JsonProperty("reuseKey")]
    public bool? ReuseKey { get; set; }

    [JsonProperty("isExpired")]
    public bool IsExpired { get; set; }

    [JsonProperty("isIssuedByAcmebot")]
    public bool IsIssuedByAcmebot { get; set; }

    [JsonProperty("isSameEndpoint")]
    public bool IsSameEndpoint { get; set; }

    [JsonProperty("acmeEndpoint")]
    public string? AcmeEndpoint { get; set; }

    [JsonProperty("dnsAlias")]
    public string? DnsAlias { get; set; }
}
