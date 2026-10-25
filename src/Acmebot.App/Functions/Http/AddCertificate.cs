using Acmebot.App.Extensions;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;
using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Acmebot.App.Functions.Http;

public partial class AddCertificate(
    IHttpContextAccessor httpContextAccessor,
    AppRoleService appRoleService,
    CertificateOperationService certificateOperationService,
    ILogger<AddCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    internal const string ReplaceExistingQueryName = "replaceExisting";

    [Function($"{nameof(AddCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificates")] HttpRequest req,
        [FromBody] CertificatePolicyItem certificatePolicyItem,
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

        if (!TryValidateModel(certificatePolicyItem))
        {
            return ValidationProblem(ModelState);
        }

        // Replacing a certificate that Acmebot does not manage (for example, one bought from another CA)
        // must be an explicit decision, so require the caller to opt in before overwriting it.
        var replaceExisting = IsReplaceExistingRequested(req);
        var existingCertificate = await certificateOperationService.FindCertificateAsync(certificatePolicyItem.CertificateName, req.HttpContext.RequestAborted);

        if (existingCertificate is not null && !existingCertificate.Properties.IsIssuedByAcmebot())
        {
            if (!replaceExisting)
            {
                return Problem(
                    $"The certificate '{certificatePolicyItem.CertificateName}' already exists in Key Vault and is not managed by Acmebot. Retry with {ReplaceExistingQueryName}=true to replace it with an ACME certificate.",
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Certificate is not managed by Acmebot");
            }

            LogUnmanagedCertificateReplacing(logger, certificatePolicyItem.CertificateName, User.Identity?.Name);
        }

        // An unmanaged certificate can still be imported before the workflow writes to Key Vault, so pass the
        // opt-in along for FinalizeOrder to recheck. Only the query parameter may grant it, never the request body.
        certificatePolicyItem.ReplaceExisting = replaceExisting;

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(CertificateIssuanceOrchestrator.IssueCertificate), certificatePolicyItem);

        LogOrchestrationStarted(logger, certificatePolicyItem.CertificateName, instanceId);

        return Accepted(Url.RouteUrl($"{nameof(GetOperation)}_{nameof(GetOperation.HttpStart)}", new { instanceId }), null);
    }

    private static bool IsReplaceExistingRequested(HttpRequest req) =>
        bool.TryParse(req.Query[ReplaceExistingQueryName], out var replaceExisting) && replaceExisting;

    [LoggerMessage(LogLevel.Warning, "Replacing a certificate that is not managed by Acmebot. CertificateName: {CertificateName}. RequestedBy: {RequestedBy}")]
    private static partial void LogUnmanagedCertificateReplacing(ILogger logger, string certificateName, string? requestedBy);

    [LoggerMessage(LogLevel.Information, "Certificate issuance orchestration started. CertificateName: {CertificateName}. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string certificateName, string instanceId);
}
