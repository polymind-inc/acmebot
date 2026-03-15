using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class GetInstanceState(IHttpContextAccessor httpContextAccessor, ILogger<GetInstanceState> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetInstanceState)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/state/{instanceId}")] HttpRequest req,
        string instanceId,
        [DurableClient] DurableTaskClient starter)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        var status = await starter.GetInstanceAsync(instanceId, getInputsAndOutputs: true);

        if (status is null)
        {
            LogInstanceStateNotFound(logger, instanceId);

            return BadRequest();
        }

        return status.RuntimeStatus switch
        {
            OrchestrationRuntimeStatus.Failed => Problem(status.SerializedOutput),
            OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending => AcceptedAtFunction($"{nameof(GetInstanceState)}_{nameof(HttpStart)}", new { instanceId }, null),
            _ => Ok()
        };
    }

    [LoggerMessage(LogLevel.Information, "Instance state lookup returned no result. InstanceId: {InstanceId}")]
    private static partial void LogInstanceStateNotFound(ILogger logger, string instanceId);
}
