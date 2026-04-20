using System.ComponentModel.DataAnnotations;

using Newtonsoft.Json;

namespace Acmebot.App.Models;

public class CertificatePolicyItem : IValidatableObject
{
    [JsonProperty("certificateName")]
    [RegularExpression("^[0-9a-zA-Z-]+$")]
    public string? CertificateName { get; set; }

    [JsonProperty("dnsNames")]
    public required string[] DnsNames { get; set; }

    [JsonProperty("dnsProviderName")]
    public string? DnsProviderName { get; set; }

    [JsonProperty("keyType")]
    [RegularExpression("^(RSA|EC)$")]
    public required string KeyType { get; set; }

    [JsonProperty("keySize")]
    public int? KeySize { get; set; }

    [JsonProperty("keyCurveName")]
    [RegularExpression(@"^P\-(256|384|521|256K)$")]
    public string? KeyCurveName { get; set; }

    [JsonProperty("reuseKey")]
    public bool? ReuseKey { get; set; }

    [JsonProperty("dnsAlias")]
    public string? DnsAlias { get; set; }

    [JsonProperty("certificateId")]
    public string? CertificateId { get; set; }

    public IEnumerable<string> AliasedDnsNames => string.IsNullOrEmpty(DnsAlias) ? DnsNames : [DnsAlias];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DnsNames.Length == 0)
        {
            yield return new ValidationResult($"The {nameof(DnsNames)} is required.", [nameof(DnsNames)]);
        }

        if (KeyType == "RSA")
        {
            if (KeySize is not (2048 or 3072 or 4096))
            {
                yield return new ValidationResult($"The {nameof(KeySize)} must be 2048, 3072, or 4096 when {nameof(KeyType)} is RSA.", [nameof(KeySize)]);
            }
        }
        else if (KeyType == "EC")
        {
            if (KeyCurveName is not ("P-256" or "P-384" or "P-521" or "P-256K"))
            {
                yield return new ValidationResult($"The {nameof(KeyCurveName)} must be P-256, P-384, P-521, or P-256K when {nameof(KeyType)} is EC.", [nameof(KeyCurveName)]);
            }
        }
    }
}
