using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Options;

using Azure.Security.KeyVault.Certificates;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Services;

public class CertificateQueryService(
    CertificateClient certificateClient,
    IOptions<AcmebotOptions> options)
{
    private readonly AcmebotOptions _options = options.Value;

    // Key Vault has no "list certificates with policy" API, so each certificate requires its own
    // GetCertificate round-trip. Fetch them in parallel with a bounded degree to avoid throttling.
    private const int MaxParallelism = 8;

    public async Task<IReadOnlyList<CertificateItem>> GetAllCertificatesAsync(CancellationToken cancellationToken = default)
    {
        var properties = new List<CertificateProperties>();

        await foreach (var certificate in certificateClient.GetPropertiesOfCertificatesAsync(cancellationToken: cancellationToken))
        {
            properties.Add(certificate);
        }

        // Preserve enumeration order by writing each result into its own slot.
        var result = new CertificateItem[properties.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, properties.Count),
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var certificate = properties[index];

                var certificateItem = (await certificateClient.GetCertificateAsync(certificate.Name, token)).Value.ToCertificateItem();

                certificateItem.IsIssuedByAcmebot = certificate.IsIssuedByAcmebot();
                certificateItem.IsSameEndpoint = certificate.IsSameEndpoint(_options.Endpoint);

                result[index] = certificateItem;
            });

        return result;
    }

    // Renewal status only needs the tag-derived flags, which are all available from the certificate
    // list. This avoids the per-certificate GetCertificate round-trips that GetAllCertificatesAsync needs.
    public async Task<IReadOnlyList<CertificateRenewalTarget>> GetRenewalTargetsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<CertificateRenewalTarget>();

        await foreach (var properties in certificateClient.GetPropertiesOfCertificatesAsync(cancellationToken: cancellationToken))
        {
            result.Add(new CertificateRenewalTarget(
                properties.Name,
                properties.Enabled != false,
                properties.IsIssuedByAcmebot(),
                properties.IsSameEndpoint(_options.Endpoint)));
        }

        return result;
    }
}

public sealed record CertificateRenewalTarget(string Name, bool Enabled, bool IsIssuedByAcmebot, bool IsSameEndpoint)
{
    public bool IsRenewable => Enabled && IsIssuedByAcmebot && IsSameEndpoint;
}
