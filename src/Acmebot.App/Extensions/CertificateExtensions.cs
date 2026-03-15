using System.Diagnostics.CodeAnalysis;

using Acmebot.App.Models;

using Azure.Security.KeyVault.Certificates;

namespace Acmebot.App.Extensions;

internal static class CertificateExtensions
{
    public static bool IsIssuedByAcmebot(this CertificateProperties properties)
    {
        return properties.Tags.TryGetIssuer(out var tagIssuer) && tagIssuer == IssuerValue;
    }

    public static bool IsSameEndpoint(this CertificateProperties properties, Uri endpoint)
    {
        return properties.Tags.TryGetEndpoint(out var tagEndpoint) && NormalizeEndpoint(tagEndpoint) == endpoint.Host;
    }

    public static CertificateItem ToCertificateItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames?.DnsNames.ToArray();

        return new CertificateItem
        {
            Id = certificate.Id,
            Name = certificate.Name,
            DnsNames = dnsNames is { Length: > 0 } ? dnsNames : [certificate.Policy.Subject[3..]],
            DnsProviderName = certificate.Properties.Tags.TryGetDnsProvider(out var dnsProviderName) ? dnsProviderName : "",
            CreatedOn = certificate.Properties.CreatedOn.GetValueOrDefault(DateTimeOffset.MinValue),
            ExpiresOn = certificate.Properties.ExpiresOn.GetValueOrDefault(DateTimeOffset.MaxValue),
            X509Thumbprint = Convert.ToHexString(certificate.Properties.X509Thumbprint),
            KeyType = certificate.Policy.KeyType.GetValueOrDefault(CertificateKeyType.Rsa).ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            IsExpired = DateTimeOffset.UtcNow > certificate.Properties.ExpiresOn.GetValueOrDefault(DateTimeOffset.MaxValue),
            AcmeEndpoint = certificate.Properties.Tags.TryGetEndpoint(out var acmeEndpoint) ? NormalizeEndpoint(acmeEndpoint) : "",
            DnsAlias = certificate.Properties.Tags.TryGetDnsAlias(out var dnsAlias) ? dnsAlias : ""
        };
    }

    public static CertificatePolicyItem ToCertificatePolicyItem(this KeyVaultCertificateWithPolicy certificate)
    {
        var dnsNames = certificate.Policy.SubjectAlternativeNames.DnsNames.ToArray();

        return new CertificatePolicyItem
        {
            CertificateName = certificate.Name,
            DnsNames = dnsNames.Length > 0 ? dnsNames : [certificate.Policy.Subject[3..]],
            DnsProviderName = certificate.Properties.Tags.TryGetDnsProvider(out var dnsProviderName) ? dnsProviderName : "",
            KeyType = certificate.Policy.KeyType.GetValueOrDefault(CertificateKeyType.Rsa).ToString(),
            KeySize = certificate.Policy.KeySize,
            KeyCurveName = certificate.Policy.KeyCurveName?.ToString(),
            ReuseKey = certificate.Policy.ReuseKey,
            DnsAlias = certificate.Properties.Tags.TryGetDnsAlias(out var dnsAlias) ? dnsAlias : ""
        };
    }

    public static IDictionary<string, string> ToCertificateMetadata(this CertificatePolicyItem certificatePolicyItem, Uri endpoint)
    {
        var metadata = new Dictionary<string, string>
        {
            { IssuerKey, IssuerValue },
            { EndpointKey, endpoint.Host },
            { DnsProviderKey, certificatePolicyItem.DnsProviderName ?? "" }
        };

        if (!string.IsNullOrEmpty(certificatePolicyItem.DnsAlias))
        {
            metadata.Add(DnsAliasKey, certificatePolicyItem.DnsAlias);
        }

        return metadata;
    }

    private const string IssuerKey = "Issuer";
    private const string EndpointKey = "Endpoint";
    private const string DnsProviderKey = "DnsProvider";
    private const string DnsAliasKey = "DnsAlias";

    private const string IssuerValue = "Acmebot";

    private static bool TryGetIssuer(this IDictionary<string, string> tags, [NotNullWhen(true)] out string? issuer) => tags.TryGetValue(IssuerKey, out issuer);

    private static bool TryGetEndpoint(this IDictionary<string, string> tags, [NotNullWhen(true)] out string? endpoint) => tags.TryGetValue(EndpointKey, out endpoint);

    private static bool TryGetDnsProvider(this IDictionary<string, string> tags, [NotNullWhen(true)] out string? dnsProviderName) => tags.TryGetValue(DnsProviderKey, out dnsProviderName);

    private static bool TryGetDnsAlias(this IDictionary<string, string> tags, [NotNullWhen(true)] out string? dnsAlias) => tags.TryGetValue(DnsAliasKey, out dnsAlias);

    private static string NormalizeEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var legacyEndpoint) ? legacyEndpoint.Host : endpoint;
}
