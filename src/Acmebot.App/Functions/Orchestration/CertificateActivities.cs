using System.Security.Cryptography.X509Certificates;

using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Options;

using Azure.Security.KeyVault.Certificates;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Orchestration;

public class CertificateActivities(
    AcmeClientFactory acmeClientFactory,
    CertificateClient certificateClient,
    IOptions<AcmebotOptions> options)
{
    private readonly AcmebotOptions _options = options.Value;

    [Function(nameof(GetRenewalCertificates))]
    public async Task<IReadOnlyList<CertificateItem>> GetRenewalCertificates([ActivityTrigger] object input)
    {
        using var acmeContext = await acmeClientFactory.CreateClientAsync();
        var acmeClient = acmeContext.Client;

        var certificateProperties = certificateClient.GetPropertiesOfCertificatesAsync();

        var result = new List<CertificateItem>();

        var now = DateTimeOffset.UtcNow;

        await foreach (var properties in certificateProperties)
        {
            if (!properties.IsIssuedByAcmebot() || !properties.IsSameEndpoint(_options.Endpoint))
            {
                continue;
            }

            var certificate = await certificateClient.GetCertificateAsync(properties.Name);

            if (acmeContext.Directory.RenewalInfo is not null)
            {
                var certificateId = X509CertificateLoader.LoadCertificate(certificate.Value.Cer).GetCertificateId();

                if (certificateId is not null)
                {
                    var renewalInfo = (await acmeClient.GetRenewalInfoAsync(certificateId)).Resource;

                    if (renewalInfo.SuggestedWindow.Start < now)
                    {
                        result.Add(certificate.Value.ToCertificateItem());

                        continue;
                    }
                }
            }

            if ((properties.ExpiresOn.GetValueOrDefault(DateTimeOffset.MaxValue) - now).TotalDays <= _options.RenewBeforeExpiry)
            {
                result.Add(certificate.Value.ToCertificateItem());
            }
        }

        return result;
    }

    [Function(nameof(GetAllCertificates))]
    public async Task<IReadOnlyList<CertificateItem>> GetAllCertificates([ActivityTrigger] object input)
    {
        var certificates = certificateClient.GetPropertiesOfCertificatesAsync();

        var result = new List<CertificateItem>();

        await foreach (var certificate in certificates)
        {
            var certificateItem = (await certificateClient.GetCertificateAsync(certificate.Name)).Value.ToCertificateItem();

            certificateItem.IsIssuedByAcmebot = certificate.IsIssuedByAcmebot();
            certificateItem.IsSameEndpoint = certificate.IsSameEndpoint(_options.Endpoint);

            result.Add(certificateItem);
        }

        return result;
    }

    [Function(nameof(GetCertificatePolicy))]
    public async Task<CertificatePolicyItem> GetCertificatePolicy([ActivityTrigger] string certificateName)
    {
        KeyVaultCertificateWithPolicy certificate = await certificateClient.GetCertificateAsync(certificateName);

        return certificate.ToCertificatePolicyItem();
    }

    [Function(nameof(RevokeCertificate))]
    public async Task RevokeCertificate([ActivityTrigger] string certificateName)
    {
        var response = await certificateClient.GetCertificateAsync(certificateName);

        using var acmeContext = await acmeClientFactory.CreateClientAsync();

        await acmeContext.Client.RevokeCertificateAsync(acmeContext.Account, response.Value.Cer);
    }
}
