using System.Text.Json;

using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;
using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class GetCertificateRenewals(
    IHttpContextAccessor httpContextAccessor,
    CertificateQueryService certificateQueryService,
    ILogger<GetCertificateRenewals> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetCertificateRenewals)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/renewals")] HttpRequest req,
        [DurableClient] DurableTaskClient starter)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var certificates = await certificateQueryService.GetRenewalTargetsAsync(req.HttpContext.RequestAborted);

        var output = await Task.WhenAll(certificates.Select(async certificate =>
        {
            var schedule = await GetRenewalSchedule(starter, certificate.Name, req.HttpContext.RequestAborted);

            return CreateRenewalItem(certificate, schedule);
        }));

        LogCertificateRenewalsRetrieved(logger, output.Length);

        return Ok(output);
    }

    private static async Task<RenewalScheduleSnapshot?> GetRenewalSchedule(DurableTaskClient starter, string certificateName, CancellationToken cancellationToken)
    {
        var instanceId = CertificateRenewalSchedulerOrchestrator.GetInstanceId(certificateName);

        var metadata = await starter.GetInstanceAsync(instanceId, getInputsAndOutputs: true, cancellationToken);

        if (metadata is null)
        {
            return null;
        }

        return new RenewalScheduleSnapshot(
            ReadSchedulerStatus(metadata),
            metadata.RuntimeStatus,
            metadata.FailureDetails?.ErrorMessage,
            metadata.LastUpdatedAt);
    }

    private static CertificateRenewalItem CreateRenewalItem(CertificateRenewalTarget certificate, RenewalScheduleSnapshot? schedule)
    {
        var state = GetRenewalState(certificate, schedule);

        return new CertificateRenewalItem
        {
            CertificateName = certificate.Name,
            Status = state.Status,
            StatusKind = state.StatusKind,
            Message = state.Message,
            NextCheck = state.NextCheck,
            LastCheckedAt = state.LastCheckedAt
        };
    }

    private static RenewalState GetRenewalState(CertificateRenewalTarget certificate, RenewalScheduleSnapshot? schedule)
    {
        var lastCheckedAt = schedule?.Status?.UpdatedAt ?? schedule?.LastUpdatedAt;

        if (!certificate.Enabled)
        {
            return new RenewalState("Disabled", "disabled", "Automatic renewal is paused because this certificate is disabled.", null, lastCheckedAt);
        }

        if (!certificate.IsIssuedByAcmebot || !certificate.IsSameEndpoint)
        {
            return new RenewalState("Not managed", "neutral", "This certificate is not managed by this Acmebot endpoint.", null, lastCheckedAt);
        }

        if (schedule is null)
        {
            return new RenewalState("Not scheduled", "pending", "Automatic renewal will start after the daily renewal check runs.", null, null);
        }

        if (schedule.RuntimeStatus is OrchestrationRuntimeStatus.Failed or OrchestrationRuntimeStatus.Terminated or OrchestrationRuntimeStatus.Suspended)
        {
            return new RenewalState("Needs attention", "attention", schedule.FailureMessage ?? "Automatic renewal is not running.", null, schedule.LastUpdatedAt);
        }

        return schedule.Status switch
        {
            { State: "Scheduled" } status => new RenewalState("Scheduled", "scheduled", status.Reason, status.NextCheck, status.UpdatedAt),
            { State: "Renewing" } status => new RenewalState("Renewing", "active", status.Reason, null, status.UpdatedAt),
            { State: "Retrying" } status => new RenewalState("Retrying", "attention", status.Reason, status.NextCheck, status.UpdatedAt),
            { State: "Stopped" } status => new RenewalState("Stopped", "attention", status.Reason, null, status.UpdatedAt),
            { State: "Checking" } status => new RenewalState("Checking", "pending", status.Reason, null, status.UpdatedAt),
            _ => new RenewalState("Checking", "pending", "Automatic renewal status is being refreshed.", null, schedule.LastUpdatedAt)
        };
    }

    private static CertificateRenewalSchedulerStatus? ReadSchedulerStatus(OrchestrationMetadata metadata)
    {
        if (metadata.SerializedCustomStatus is null)
        {
            return null;
        }

        try
        {
            return metadata.ReadCustomStatusAs<CertificateRenewalSchedulerStatus>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record RenewalScheduleSnapshot(
        CertificateRenewalSchedulerStatus? Status,
        OrchestrationRuntimeStatus RuntimeStatus,
        string? FailureMessage,
        DateTimeOffset LastUpdatedAt);

    private sealed record RenewalState(
        string Status,
        string StatusKind,
        string Message,
        DateTimeOffset? NextCheck,
        DateTimeOffset? LastCheckedAt);

    [LoggerMessage(LogLevel.Information, "Certificate renewals retrieved. Count: {Count}")]
    private static partial void LogCertificateRenewalsRetrieved(ILogger logger, int count);
}
