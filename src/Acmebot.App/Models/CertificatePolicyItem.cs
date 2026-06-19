using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Acmebot.App.Models;

public class CertificatePolicyItem : IValidatableObject
{
    [JsonPropertyName("certificateName")]
    [RegularExpression("^[0-9A-Za-z-]{1,127}$")]
    public string? CertificateName { get; set; }

    [JsonPropertyName("dnsNames")]
    public required string[] DnsNames { get; set; }

    [JsonPropertyName("dnsProviderName")]
    public required string DnsProviderName { get; set; }

    [JsonPropertyName("keyType")]
    [RegularExpression("^(RSA|EC)$")]
    public required string KeyType { get; set; }

    [JsonPropertyName("keySize")]
    public int? KeySize { get; set; }

    [JsonPropertyName("keyCurveName")]
    [RegularExpression(@"^P\-(256|384|521|256K)$")]
    public string? KeyCurveName { get; set; }

    [JsonPropertyName("reuseKey")]
    public bool? ReuseKey { get; set; }

    [JsonPropertyName("dnsAlias")]
    public string? DnsAlias { get; set; }

    [JsonPropertyName("tags")]
    public IDictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("certificateId")]
    public string? CertificateId { get; set; }

    public IEnumerable<string> AliasedDnsNames => string.IsNullOrEmpty(DnsAlias) ? DnsNames : [DnsAlias];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DnsNames.Length == 0)
        {
            yield return new ValidationResult($"The {nameof(DnsNames)} is required.", [nameof(DnsNames)]);
        }

        if (string.IsNullOrWhiteSpace(DnsProviderName))
        {
            yield return new ValidationResult($"The {nameof(DnsProviderName)} is required.", [nameof(DnsProviderName)]);
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

        if (Tags is not null)
        {
            var tagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in Tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Key))
                {
                    yield return new ValidationResult($"The {nameof(Tags)} contains an empty tag name.", [nameof(Tags)]);
                    continue;
                }

                var tagName = tag.Key.Trim();

                if (string.Equals(tagName, "Acmebot", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new ValidationResult($"The Acmebot tag is reserved for internal metadata.", [nameof(Tags)]);
                }

                if (!tagNames.Add(tagName))
                {
                    yield return new ValidationResult($"The {nameof(Tags)} contains duplicate tag names.", [nameof(Tags)]);
                }
            }
        }
    }
}
