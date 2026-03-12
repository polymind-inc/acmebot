using Acmebot.App.Models;

using Azure.Security.KeyVault.Certificates;

namespace Acmebot.App.Internal;

internal static class CertificatePolicyExtensions
{
    public static CertificatePolicy ToCertificatePolicy(this CertificatePolicyItem certificatePolicyItem)
    {
        var subjectAlternativeNames = new SubjectAlternativeNames();

        foreach (var dnsName in certificatePolicyItem.DnsNames)
        {
            subjectAlternativeNames.DnsNames.Add(dnsName);
        }

        var certificatePolicy = new CertificatePolicy(WellKnownIssuerNames.Unknown, $"CN={certificatePolicyItem.DnsNames[0]}", subjectAlternativeNames)
        {
            KeySize = certificatePolicyItem.KeySize,
            ReuseKey = certificatePolicyItem.ReuseKey,
            EnhancedKeyUsage =
            {
                "1.3.6.1.5.5.7.3.1"
            }
        };

        if (!string.IsNullOrEmpty(certificatePolicyItem.KeyType))
        {
            certificatePolicy.KeyType = certificatePolicyItem.KeyType;
        }

        if (!string.IsNullOrEmpty(certificatePolicyItem.KeyCurveName))
        {
            certificatePolicy.KeyCurveName = certificatePolicyItem.KeyCurveName;
        }

        return certificatePolicy;
    }
}
