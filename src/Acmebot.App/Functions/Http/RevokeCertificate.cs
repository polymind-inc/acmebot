using Acmebot.App.Extensions;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class RevokeCertificate(IHttpContextAccessor httpContextAccessor, ILogger<RevokeCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(RevokeCertificate)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context, string certificateName)
    {
        if (string.IsNullOrEmpty(certificateName))
        {
            return;
        }

        await context.CallRevokeCertificateAsync(certificateName);
    }

    [Function($"{nameof(RevokeCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate/{certificateName}/revoke")] HttpRequest req,
        string certificateName,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        if (!User.HasRevokeCertificateRole())
        {
            return Forbid();
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(RevokeCertificate)}_{nameof(Orchestrator)}", certificateName);

        LogOrchestrationStarted(logger, certificateName, instanceId);

        var metadata = await starter.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

        return Ok(metadata.SerializedOutput);
    }

    [LoggerMessage(LogLevel.Information, "Certificate revocation orchestration started. CertificateName: {CertificateName}. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string certificateName, string instanceId);
}
