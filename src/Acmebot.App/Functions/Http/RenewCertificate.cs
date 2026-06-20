using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class RenewCertificate(
    IHttpContextAccessor httpContextAccessor,
    AppRoleService appRoleService,
    CertificateOperationService certificateOperationService,
    ILogger<RenewCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(RenewCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificates/{certificateName}/renew")] HttpRequest req,
        string certificateName,
        [DurableClient] DurableTaskClient starter)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (!appRoleService.HasIssueCertificateRole(User))
        {
            return Forbid();
        }

        var certificatePolicyItem = await certificateOperationService.GetCertificatePolicyAsync(certificateName, req.HttpContext.RequestAborted);
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(CertificateIssuanceOrchestrator.IssueCertificate), certificatePolicyItem);

        LogOrchestrationStarted(logger, certificateName, instanceId);

        return AcceptedAtFunction($"{nameof(GetOperation)}_{nameof(GetOperation.HttpStart)}", new { instanceId }, null);
    }

    [LoggerMessage(LogLevel.Information, "Certificate renewal orchestration started. CertificateName: {CertificateName}. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string certificateName, string instanceId);
}
