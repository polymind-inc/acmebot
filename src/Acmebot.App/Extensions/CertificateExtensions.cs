using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Models;

using Azure.Security.KeyVault.Certificates;

namespace Acmebot.App.Extensions;

internal static class CertificateExtensions
{
    public static bool IsIssuedByAcmebot(this CertificateProperties properties)
    {
        if (properties.Tags.ContainsKey(AcmebotTagKey))
        {
            return true;
        }

        return properties.Tags.TryGetValue(LegacyIssuerKey, out var issuer) && issuer == IssuerValue;
    }

    public static bool IsSameEndpoint(this CertificateProperties properties, Uri endpoint)
    {
        var metadata = properties.Tags.GetAcmebotMetadata();

        return metadata is not null && !string.IsNullOrEmpty(metadata.Endpoint) && NormalizeEndpoint(metadata.Endpoint) == endpoint.Host;
    }

    public static CertificateItem ToCertificateItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames?.DnsNames.ToArray();
        var metadata = certificate.Properties.Tags.GetAcmebotMetadata();

        return new CertificateItem
        {
            Id = certificate.Id,
            Name = certificate.Name,
            DnsNames = dnsNames is { Length: > 0 } ? dnsNames : [certificate.Policy.Subject[3..]],
            DnsProviderName = metadata?.DnsProvider ?? "",
            CreatedOn = certificate.Properties.CreatedOn.GetValueOrDefault(DateTimeOffset.MinValue),
            ExpiresOn = certificate.Properties.ExpiresOn.GetValueOrDefault(DateTimeOffset.MaxValue),
            X509Thumbprint = Convert.ToHexString(certificate.Properties.X509Thumbprint),
            KeyType = certificate.Policy.KeyType.GetValueOrDefault(CertificateKeyType.Rsa).ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            Enabled = certificate.Properties.Enabled != false,
            AcmeEndpoint = !string.IsNullOrEmpty(metadata?.Endpoint) ? NormalizeEndpoint(metadata.Endpoint) : "",
            DnsAlias = metadata?.DnsAlias ?? "",
            Tags = certificate.Properties.Tags.GetCustomCertificateTags()
        };
    }

    public static CertificatePolicyItem ToCertificatePolicyItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames.DnsNames.ToArray();
        var metadata = certificate.Properties.Tags.GetAcmebotMetadata();

        return new CertificatePolicyItem
        {
            CertificateName = certificate.Name,
            DnsNames = dnsNames.Length > 0 ? dnsNames : [certificate.Policy.Subject[3..]],
            DnsProviderName = metadata?.DnsProvider ?? "",
            KeyType = certificate.Policy.KeyType.GetValueOrDefault(CertificateKeyType.Rsa).ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            DnsAlias = metadata?.DnsAlias ?? "",
            CertificateId = metadata?.CertificateId,
            Tags = certificate.Properties.Tags.GetCustomCertificateTags()
        };
    }

    public static IDictionary<string, string> ToCertificateTags(this CertificatePolicyItem certificatePolicyItem, Uri endpoint)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (certificatePolicyItem.Tags is not null)
        {
            foreach (var tag in certificatePolicyItem.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Key) || string.Equals(tag.Key.Trim(), AcmebotTagKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                tags[tag.Key.Trim()] = tag.Value.Trim();
            }
        }

        var metadata = new AcmebotCertificateMetadata
        {
            Endpoint = endpoint.Host,
            DnsProvider = certificatePolicyItem.DnsProviderName,
            DnsAlias = string.IsNullOrEmpty(certificatePolicyItem.DnsAlias) ? null : certificatePolicyItem.DnsAlias
        };

        tags[AcmebotTagKey] = JsonSerializer.Serialize(metadata, s_jsonOptions);

        return tags;
    }

    public static void SetCertificateId(this IDictionary<string, string> tags, string certificateId)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentException.ThrowIfNullOrEmpty(certificateId);

        var metadata = tags.GetAcmebotMetadata() ?? new AcmebotCertificateMetadata();

        metadata.CertificateId = certificateId;

        tags[AcmebotTagKey] = JsonSerializer.Serialize(metadata, s_jsonOptions);
    }

    public static bool TryGetCertificateId(this CertificateProperties properties, [NotNullWhen(true)] out string? certificateId)
    {
        var metadata = properties.Tags.GetAcmebotMetadata();

        certificateId = metadata?.CertificateId;

        return !string.IsNullOrEmpty(certificateId);
    }

    private const string AcmebotTagKey = "Acmebot";

    // Legacy tag keys (kept for backward compatibility when reading existing certificates)
    private const string LegacyIssuerKey = "Issuer";
    private const string LegacyEndpointKey = "Endpoint";
    private const string LegacyDnsProviderKey = "DnsProvider";
    private const string LegacyDnsAliasKey = "DnsAlias";

    private const string IssuerValue = "Acmebot";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static AcmebotCertificateMetadata? GetAcmebotMetadata(this IDictionary<string, string> tags)
    {
        if (tags.TryGetValue(AcmebotTagKey, out var json) && !string.IsNullOrEmpty(json))
        {
            try
            {
                return JsonSerializer.Deserialize<AcmebotCertificateMetadata>(json, s_jsonOptions);
            }
            catch (JsonException)
            {
                // Fall through to legacy tag parsing
            }
        }

        if (tags.ContainsKey(LegacyIssuerKey))
        {
            tags.TryGetValue(LegacyEndpointKey, out var endpoint);
            tags.TryGetValue(LegacyDnsProviderKey, out var dnsProvider);
            tags.TryGetValue(LegacyDnsAliasKey, out var dnsAlias);

            return new AcmebotCertificateMetadata
            {
                Endpoint = endpoint,
                DnsProvider = dnsProvider,
                DnsAlias = string.IsNullOrEmpty(dnsAlias) ? null : dnsAlias
            };
        }

        return null;
    }

    private static Dictionary<string, string> GetCustomCertificateTags(this IDictionary<string, string> tags)
    {
        string[] internalTagKeys = tags.Keys.Contains(AcmebotTagKey, StringComparer.OrdinalIgnoreCase)
            ? [AcmebotTagKey]
            : [AcmebotTagKey, LegacyIssuerKey, LegacyEndpointKey, LegacyDnsProviderKey, LegacyDnsAliasKey];

        return tags
            .Where(tag => !internalTagKeys.Contains(tag.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(tag => tag.Key, tag => tag.Value);
    }

    private static string NormalizeEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var legacyEndpoint) ? legacyEndpoint.Host : endpoint;

    private sealed class AcmebotCertificateMetadata
    {
        [JsonPropertyName("endpoint")]
        public string? Endpoint { get; init; }

        [JsonPropertyName("dnsProvider")]
        public string? DnsProvider { get; init; }

        [JsonPropertyName("dnsAlias")]
        public string? DnsAlias { get; init; }

        [JsonPropertyName("certificateId")]
        public string? CertificateId { get; set; }
    }
}
