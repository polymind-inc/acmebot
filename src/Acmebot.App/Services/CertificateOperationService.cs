using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;

using Azure.Security.KeyVault.Certificates;

namespace Acmebot.App.Services;

public class CertificateOperationService(
    AcmeClientFactory acmeClientFactory,
    CertificateClient certificateClient)
{
    public async Task<CertificatePolicyItem> GetCertificatePolicyAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        KeyVaultCertificateWithPolicy certificate = await certificateClient.GetCertificateAsync(certificateName, cancellationToken);

        return certificate.ToCertificatePolicyItem();
    }

    public async Task RevokeCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        var response = await certificateClient.GetCertificateAsync(certificateName, cancellationToken);

        var acmeContext = await acmeClientFactory.CreateClientAsync();

        await acmeContext.Client.RevokeCertificateAsync(acmeContext.Account, response.Value.Cer, cancellationToken: cancellationToken);

        response.Value.Properties.Enabled = false;

        await certificateClient.UpdateCertificatePropertiesAsync(response.Value.Properties, cancellationToken);
    }
}
