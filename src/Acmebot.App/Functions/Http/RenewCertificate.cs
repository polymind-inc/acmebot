using Acmebot.App.Extensions;
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
        if (!IsAuthenticationEnabled || User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (!appRoleService.HasIssueCertificateRole(User))
        {
            return Forbid();
        }

        var certificate = await certificateOperationService.FindCertificateAsync(certificateName, req.HttpContext.RequestAborted);

        if (certificate is null)
        {
            return Problem($"The certificate '{certificateName}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // Unmanaged certificates have no DNS provider metadata to renew with. Switching one to an ACME
        // certificate is an explicit replacement that goes through AddCertificate instead.
        if (!certificate.Properties.IsIssuedByAcmebot())
        {
            return Problem(
                $"The certificate '{certificateName}' is not managed by Acmebot. Issue a new certificate with the same name and {AddCertificate.ReplaceExistingQueryName}=true to replace it with an ACME certificate.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Certificate is not managed by Acmebot");
        }

        var certificatePolicyItem = certificate.ToCertificatePolicyItem();

        // Manual renewal must also work after the Acmebot account has changed. In that case, the
        // current account cannot use the ARI certificate identifier issued to the previous account.
        certificatePolicyItem.CertificateId = null;

        // Renewal never authorizes replacement. If an unmanaged certificate is imported under this name while
        // the workflow runs, FinalizeOrder stops instead of overwriting it.
        certificatePolicyItem.ReplaceExisting = false;

        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(CertificateIssuanceOrchestrator.IssueCertificate), certificatePolicyItem);

        LogOrchestrationStarted(logger, certificateName, instanceId);

        return Accepted(Url.RouteUrl($"{nameof(GetOperation)}_{nameof(GetOperation.HttpStart)}", new { instanceId }), null);
    }

    [LoggerMessage(LogLevel.Information, "Certificate renewal orchestration started. CertificateName: {CertificateName}. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string certificateName, string instanceId);
}
