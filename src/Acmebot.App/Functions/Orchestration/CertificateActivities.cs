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

    [Function(nameof(EvaluateCertificateRenewal))]
    public async Task<CertificateRenewalEvaluation> EvaluateCertificateRenewal([ActivityTrigger] CertificateRenewalSchedulerState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var now = DateTimeOffset.UtcNow;
        KeyVaultCertificateWithPolicy certificate;

        try
        {
            certificate = await certificateClient.GetCertificateAsync(state.CertificateName);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return new CertificateRenewalEvaluation
            {
                IsActive = false,
                ShouldRenew = false,
                NextCheck = now,
                Reason = "Certificate was not found."
            };
        }

        var properties = certificate.Properties;

        if (properties.Enabled == false)
        {
            return new CertificateRenewalEvaluation
            {
                IsActive = false,
                ShouldRenew = false,
                NextCheck = now,
                Reason = "Certificate is disabled."
            };
        }

        if (!properties.IsIssuedByAcmebot() || !properties.IsSameEndpoint(_options.Endpoint))
        {
            return new CertificateRenewalEvaluation
            {
                IsActive = false,
                ShouldRenew = false,
                NextCheck = now,
                Reason = "Certificate is not managed by this Acmebot endpoint."
            };
        }

        if (properties.TryGetCertificateId(out var certificateId))
        {
            using var acmeContext = await acmeClientFactory.CreateClientAsync();

            if (acmeContext.Directory.RenewalInfo is not null)
            {
                try
                {
                    var renewalInfo = await acmeContext.Client.GetRenewalInfoAsync(certificateId);

                    return new CertificateRenewalEvaluation
                    {
                        IsActive = true,
                        ShouldRenew = renewalInfo.Resource.SuggestedWindow.Start <= now,
                        NextCheck = now.Add(renewalInfo.RetryAfter ?? TimeSpan.FromDays(1)),
                        Reason = "ARI"
                    };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Fall back to local scheduling below and check renewalInfo again later.
                    return new CertificateRenewalEvaluation
                    {
                        IsActive = true,
                        ShouldRenew = CheckShouldRenew(properties, now),
                        NextCheck = now.Add(TimeSpan.FromDays(1)),
                        Reason = "ARI unavailable"
                    };
                }
            }
        }

        return new CertificateRenewalEvaluation
        {
            IsActive = true,
            ShouldRenew = CheckShouldRenew(properties, now),
            NextCheck = now.AddDays(1),
            Reason = "Schedule"
        };
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

        response.Value.Properties.Enabled = false;

        await certificateClient.UpdateCertificatePropertiesAsync(response.Value.Properties);
    }

    private bool CheckShouldRenew(CertificateProperties properties, DateTimeOffset now)
    {
        if (properties.ExpiresOn is not { } expiresOn)
        {
            return false;
        }

        var notBefore = properties.NotBefore ?? properties.CreatedOn;

        if (expiresOn <= now)
        {
            return true;
        }

        if (notBefore is null || notBefore.Value > expiresOn)
        {
            return false;
        }

        var lifetime = expiresOn - notBefore.Value;
        var renewalThreshold = TimeSpan.FromTicks((long)(lifetime.Ticks * (_options.RenewBeforeExpiry / 100d)));
        var suggestedWindowStart = expiresOn - renewalThreshold;

        return suggestedWindowStart <= now;
    }
}
