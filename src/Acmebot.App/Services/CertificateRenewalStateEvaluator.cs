using Acmebot.App.Models;

using Microsoft.DurableTask.Client;

namespace Acmebot.App.Services;

internal static class CertificateRenewalStateEvaluator
{
    public static CertificateRenewalState GetRenewalState(CertificateRenewalTarget certificate, CertificateRenewalScheduleSnapshot? schedule)
    {
        var lastCheckedAt = schedule?.Status?.UpdatedAt ?? schedule?.LastUpdatedAt;

        if (!certificate.Enabled)
        {
            return new CertificateRenewalState("Disabled", "disabled", "Automatic renewal is paused because this certificate is disabled.", null, lastCheckedAt);
        }

        if (!certificate.IsIssuedByAcmebot || !certificate.IsSameEndpoint)
        {
            return new CertificateRenewalState("Not managed", "neutral", "This certificate is not managed by this Acmebot endpoint.", null, lastCheckedAt);
        }

        if (schedule is null)
        {
            return new CertificateRenewalState("Not scheduled", "pending", "Automatic renewal will start after the daily renewal check runs.", null, null);
        }

        if (schedule.RuntimeStatus is OrchestrationRuntimeStatus.Failed or OrchestrationRuntimeStatus.Terminated or OrchestrationRuntimeStatus.Suspended)
        {
            return new CertificateRenewalState("Needs attention", "attention", schedule.FailureMessage ?? "Automatic renewal is not running.", null, schedule.LastUpdatedAt);
        }

        return schedule.Status switch
        {
            { State: "Scheduled" } status => new CertificateRenewalState("Scheduled", "scheduled", status.Reason, status.NextCheck, status.UpdatedAt),
            { State: "Renewing" } status => new CertificateRenewalState("Renewing", "active", status.Reason, null, status.UpdatedAt),
            { State: "Retrying" } status => new CertificateRenewalState("Retrying", "attention", status.Reason, status.NextCheck, status.UpdatedAt),
            { State: "Stopped" } status => new CertificateRenewalState("Stopped", "attention", status.Reason, null, status.UpdatedAt),
            { State: "Checking" } status => new CertificateRenewalState("Checking", "pending", status.Reason, null, status.UpdatedAt),
            _ => new CertificateRenewalState("Checking", "pending", "Automatic renewal status is being refreshed.", null, schedule.LastUpdatedAt)
        };
    }
}
