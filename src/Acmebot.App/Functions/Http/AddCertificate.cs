using Acmebot.App.Extensions;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Acmebot.App.Functions.Http;

public partial class AddCertificate(IHttpContextAccessor httpContextAccessor, ILogger<AddCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(AddCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate")] HttpRequest req,
        [FromBody] CertificatePolicyItem certificatePolicyItem,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        if (!User.HasIssueCertificateRole())
        {
            return Forbid();
        }

        if (!TryValidateModel(certificatePolicyItem))
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrEmpty(certificatePolicyItem.CertificateName))
        {
            certificatePolicyItem.CertificateName = certificatePolicyItem.DnsNames[0].Replace("*", "wildcard").Replace(".", "-");
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);

        LogOrchestrationStarted(logger, certificatePolicyItem.CertificateName, instanceId);

        return AcceptedAtFunction($"{nameof(GetInstanceState)}_{nameof(GetInstanceState.HttpStart)}", new { instanceId }, null);
    }

    [LoggerMessage(LogLevel.Information, "Certificate issuance orchestration started. CertificateName: {CertificateName}. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string certificateName, string instanceId);
}
