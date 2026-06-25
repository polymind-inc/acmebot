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

    private static async Task<CertificateRenewalScheduleSnapshot?> GetRenewalSchedule(DurableTaskClient starter, string certificateName, CancellationToken cancellationToken)
    {
        var instanceId = CertificateRenewalSchedulerOrchestrator.GetInstanceId(certificateName);

        var metadata = await starter.GetInstanceAsync(instanceId, getInputsAndOutputs: true, cancellationToken);

        if (metadata is null)
        {
            return null;
        }

        return new CertificateRenewalScheduleSnapshot(
            ReadSchedulerStatus(metadata),
            metadata.RuntimeStatus,
            metadata.FailureDetails?.ErrorMessage,
            metadata.LastUpdatedAt);
    }

    private static CertificateRenewalItem CreateRenewalItem(CertificateRenewalTarget certificate, CertificateRenewalScheduleSnapshot? schedule)
    {
        var state = CertificateRenewalStateEvaluator.GetRenewalState(certificate, schedule);

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

    [LoggerMessage(LogLevel.Information, "Certificate renewals retrieved. Count: {Count}")]
    private static partial void LogCertificateRenewalsRetrieved(ILogger logger, int count);
}
