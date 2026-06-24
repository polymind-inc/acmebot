using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Acmebot.App.Models;

public partial class CertificatePolicyItem : IValidatableObject
{
    [JsonPropertyName("certificateName")]
    public required string CertificateName { get; set; }

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

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("tags")]
    public IDictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("certificateId")]
    public string? CertificateId { get; set; }

    public IEnumerable<string> AliasedDnsNames => string.IsNullOrEmpty(DnsAlias) ? DnsNames : [DnsAlias];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DnsNames is not { Length: > 0 })
        {
            yield return new ValidationResult($"The {nameof(DnsNames)} is required.", [nameof(DnsNames)]);
        }

        foreach (var result in ValidateCertificateName())
        {
            yield return result;
        }

        if (string.IsNullOrWhiteSpace(DnsProviderName))
        {
            yield return new ValidationResult($"The {nameof(DnsProviderName)} is required.", [nameof(DnsProviderName)]);
        }

        foreach (var result in ValidateDnsNames())
        {
            yield return result;
        }

        foreach (var result in ValidateDnsAlias())
        {
            yield return result;
        }

        foreach (var result in ValidateKeyOptions())
        {
            yield return result;
        }

        foreach (var result in ValidateTags())
        {
            yield return result;
        }
    }

    private IEnumerable<ValidationResult> ValidateCertificateName()
    {
        if (string.IsNullOrWhiteSpace(CertificateName))
        {
            yield return new ValidationResult($"The {nameof(CertificateName)} is required.", [nameof(CertificateName)]);
            yield break;
        }

        if (!CertificateNameRegex().IsMatch(CertificateName))
        {
            yield return new ValidationResult($"The {nameof(CertificateName)} must be 1 to 127 characters and contain only letters, numbers, and hyphens.", [nameof(CertificateName)]);
        }
    }

    [GeneratedRegex("^[0-9A-Za-z-]{1,127}$")]
    private static partial Regex CertificateNameRegex();

    private IEnumerable<ValidationResult> ValidateDnsNames()
    {
        if (DnsNames is not { Length: > 0 })
        {
            yield break;
        }

        foreach (var dnsName in DnsNames)
        {
            var message = ValidateDnsName(dnsName, nameof(DnsNames), allowWildcard: true);

            if (message is not null)
            {
                yield return new ValidationResult(message, [nameof(DnsNames)]);
            }
        }
    }

    private IEnumerable<ValidationResult> ValidateDnsAlias()
    {
        if (string.IsNullOrEmpty(DnsAlias))
        {
            yield break;
        }

        var message = ValidateDnsName(DnsAlias, nameof(DnsAlias), allowWildcard: false);

        if (message is not null)
        {
            yield return new ValidationResult(message, [nameof(DnsAlias)]);
        }
    }

    private static string? ValidateDnsName(string? value, string fieldName, bool allowWildcard)
    {
        if (string.IsNullOrEmpty(value))
        {
            return $"The {fieldName} contains an empty DNS name.";
        }

        if (value.Length > 253)
        {
            return $"The {fieldName} must be 253 characters or fewer.";
        }

        var rawLabels = value.Split('.');

        if (rawLabels.Length < 2)
        {
            return $"The {fieldName} must include a domain suffix.";
        }

        for (var labelIndex = 0; labelIndex < rawLabels.Length; labelIndex++)
        {
            var rawLabel = rawLabels[labelIndex];

            if (rawLabel.Length == 0)
            {
                return $"The {fieldName} cannot contain empty DNS labels.";
            }

            if (rawLabel == "*")
            {
                if (!allowWildcard)
                {
                    return $"The {fieldName} cannot be a wildcard.";
                }

                if (labelIndex != 0)
                {
                    return "A wildcard can only be the leftmost DNS label.";
                }

                continue;
            }

            if (rawLabel.Length > 63)
            {
                return "Each DNS label must be 63 characters or fewer.";
            }

            if (rawLabel.Contains('*') || !DnsLabelRegex().IsMatch(rawLabel))
            {
                return allowWildcard
                    ? $"The {fieldName} must be an ASCII DNS name containing only letters, numbers, hyphens, dots, and a leftmost wildcard."
                    : $"The {fieldName} must be an ASCII DNS name containing only letters, numbers, hyphens, and dots.";
            }

        }

        return null;
    }

    [GeneratedRegex("^[0-9A-Za-z](?:[0-9A-Za-z-]{0,61}[0-9A-Za-z])?$")]
    private static partial Regex DnsLabelRegex();

    private IEnumerable<ValidationResult> ValidateKeyOptions()
    {
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

    private IEnumerable<ValidationResult> ValidateTags()
    {
        if (Tags is null)
        {
            yield break;
        }

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
