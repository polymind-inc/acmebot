using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Options;
using Acmebot.App.Services;

using Azure.Security.KeyVault.Certificates;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Orchestration;

public class CertificateActivities(
    AcmeClientFactory acmeClientFactory,
    CertificateClient certificateClient,
    CertificateOperationService certificateOperationService,
    IOptions<AcmebotOptions> options)
{
    private readonly AcmebotOptions _options = options.Value;

    // How long to wait before checking the ACME renewal information (ARI) again when it cannot be used right now.
    private static readonly TimeSpan s_renewalInfoCheckInterval = TimeSpan.FromHours(6);

    [Function(nameof(EvaluateCertificateRenewal))]
    public async Task<CertificateRenewalEvaluation> EvaluateCertificateRenewal([ActivityTrigger] string certificateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateName);

        var now = DateTimeOffset.UtcNow;
        KeyVaultCertificateWithPolicy certificate;

        try
        {
            certificate = await certificateClient.GetCertificateAsync(certificateName);
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

        // RFC 9773 Section 4.3: renewal information must not be checked after the certificate
        // has expired, so renew immediately without consulting the ARI endpoint.
        if (properties.ExpiresOn is { } expiresOn && expiresOn <= now)
        {
            return new CertificateRenewalEvaluation
            {
                IsActive = true,
                ShouldRenew = true,
                NextCheck = now.Add(s_renewalInfoCheckInterval),
                Reason = "Certificate has expired. Renewing immediately."
            };
        }

        if (properties.TryGetCertificateId(out var certificateId))
        {
            var acmeContext = await acmeClientFactory.CreateClientAsync();

            if (acmeContext.Directory.RenewalInfo is not null)
            {
                try
                {
                    var renewalInfo = await acmeContext.Client.GetRenewalInfoAsync(certificateId);
                    var suggestedWindow = renewalInfo.Resource.SuggestedWindow;
                    var renewalTime = CertificateRenewalScheduleEvaluator.SelectRenewalTime(certificateId, suggestedWindow.Start, suggestedWindow.End);

                    return new CertificateRenewalEvaluation
                    {
                        IsActive = true,
                        ShouldRenew = renewalTime <= now,
                        NextCheck = SelectNextCheck(now, renewalTime, renewalInfo.RetryAfter),
                        Reason = "Renewal is scheduled within the certificate authority's suggested renewal window."
                    };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Fall back to local scheduling below and check renewalInfo again later.
                    return new CertificateRenewalEvaluation
                    {
                        IsActive = true,
                        ShouldRenew = CertificateRenewalScheduleEvaluator.ShouldRenew(properties.NotBefore, properties.CreatedOn, properties.ExpiresOn, _options.RenewBeforeExpiry, now),
                        NextCheck = now.Add(s_renewalInfoCheckInterval),
                        Reason = "Renewal information is temporarily unavailable. Using the configured schedule and rechecking soon."
                    };
                }
            }
        }

        return new CertificateRenewalEvaluation
        {
            IsActive = true,
            ShouldRenew = CertificateRenewalScheduleEvaluator.ShouldRenew(properties.NotBefore, properties.CreatedOn, properties.ExpiresOn, _options.RenewBeforeExpiry, now),
            NextCheck = now.AddDays(1),
            Reason = "Renewal is scheduled based on the configured renewal threshold."
        };
    }

    [Function(nameof(GetCertificatePolicy))]
    public async Task<CertificatePolicyItem> GetCertificatePolicy([ActivityTrigger] string certificateName)
    {
        try
        {
            return await certificateOperationService.GetCertificatePolicyAsync(certificateName);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // The certificate was deleted after renewal evaluation. Surface this as a precondition failure
            // so the scheduler can stop instead of treating it as a retriable renewal error.
            throw new PreconditionException("Certificate was not found.", ex);
        }
    }

    private static DateTimeOffset SelectNextCheck(DateTimeOffset now, DateTimeOffset renewalTime, TimeSpan? retryAfter)
    {
        return CertificateRenewalScheduleEvaluator.SelectNextCheck(now, renewalTime, retryAfter, s_renewalInfoCheckInterval);
    }
}
