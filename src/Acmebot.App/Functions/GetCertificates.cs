using Acmebot.App.Models;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions;

public partial class GetCertificates(IHttpContextAccessor httpContextAccessor, ILogger<GetCertificates> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetCertificates)}_{nameof(Orchestrator)}")]
    public Task<IReadOnlyList<CertificateItem>> Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return context.CallGetAllCertificatesAsync(null!);
    }

    [Function($"{nameof(GetCertificates)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/certificates")] HttpRequest req,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(GetCertificates)}_{nameof(Orchestrator)}");

        LogOrchestrationStarted(logger, instanceId);

        var metadata = await starter.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

        return Ok(metadata.SerializedOutput);
    }

    [LoggerMessage(LogLevel.Information, "Certificate list retrieval orchestration started. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string instanceId);
}
