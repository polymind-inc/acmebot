using Acmebot.Internal;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.Functions;

public class RenewCertificate(IHttpContextAccessor httpContextAccessor, ILogger<RenewCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(RenewCertificate)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var certificateName = context.GetInput<string>();

        if (string.IsNullOrEmpty(certificateName))
        {
            return;
        }

        // 証明書の更新処理を開始
        var certificatePolicyItem = await context.CallGetCertificatePolicyAsync(certificateName);

        await context.CallSubOrchestratorAsync(nameof(SharedOrchestrator.IssueCertificate), certificatePolicyItem);
    }

    [Function($"{nameof(RenewCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificate/{certificateName}/renew")] HttpRequest req,
        string certificateName,
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

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(RenewCertificate)}_{nameof(Orchestrator)}", certificateName);

        logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        return AcceptedAtFunction($"{nameof(GetInstanceState)}_{nameof(GetInstanceState.HttpStart)}", new { instanceId }, null);
    }
}
