using Acmebot.Internal;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.Functions;

public class RevokeCertificate(IHttpContextAccessor httpContextAccessor, ILogger<RevokeCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(RevokeCertificate)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var certificateName = context.GetInput<string>();

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

        logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        var metadata = await starter.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

        return Ok(metadata.SerializedOutput);
    }
}
