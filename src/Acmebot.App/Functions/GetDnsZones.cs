using Acmebot.App.Models;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions;

public partial class GetDnsZones(IHttpContextAccessor httpContextAccessor, ILogger<GetDnsZones> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetDnsZones)}_{nameof(Orchestrator)}")]
    public Task<IReadOnlyList<DnsZoneGroup>> Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        return context.CallGetAllDnsZonesAsync(null!);
    }

    [Function($"{nameof(GetDnsZones)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/dns-zones")] HttpRequest req,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(GetDnsZones)}_{nameof(Orchestrator)}");

        LogOrchestrationStarted(logger, instanceId);

        var metadata = await starter.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true);

        return Ok(metadata.SerializedOutput);
    }

    [LoggerMessage(LogLevel.Information, "DNS zone retrieval orchestration started. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string instanceId);
}
