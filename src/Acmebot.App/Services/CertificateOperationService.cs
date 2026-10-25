using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;

using Azure;
using Azure.Security.KeyVault.Certificates;

namespace Acmebot.App.Services;

public class CertificateOperationService(
    AcmeClientFactory acmeClientFactory,
    CertificateClient certificateClient)
{
    public async Task<KeyVaultCertificateWithPolicy?> FindCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        try
        {
            return await certificateClient.GetCertificateAsync(certificateName, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<CertificatePolicyItem> GetCertificatePolicyAsync(string certificateName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        KeyVaultCertificateWithPolicy certificate = await certificateClient.GetCertificateAsync(certificateName, cancellationToken);

        return certificate.ToCertificatePolicyItem();
    }

    public async Task RevokeCertificateAsync(KeyVaultCertificateWithPolicy certificate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var acmeContext = await acmeClientFactory.CreateClientAsync();

        await acmeContext.Client.RevokeCertificateAsync(acmeContext.Account, certificate.Cer, cancellationToken: cancellationToken);

        certificate.Properties.Enabled = false;

        await certificateClient.UpdateCertificatePropertiesAsync(certificate.Properties, cancellationToken);
    }
}
