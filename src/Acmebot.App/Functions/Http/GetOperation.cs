using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class GetOperation(IHttpContextAccessor httpContextAccessor, ILogger<GetOperation> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetOperation)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/operations/{instanceId}")] HttpRequest req,
        string instanceId,
        [DurableClient] DurableTaskClient starter)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var metadata = await starter.GetInstanceAsync(instanceId, getInputsAndOutputs: true);

        if (metadata is null)
        {
            LogInstanceStateNotFound(logger, instanceId);

            return BadRequest();
        }

        return metadata.RuntimeStatus switch
        {
            OrchestrationRuntimeStatus.Failed => Problem(metadata.FailureDetails?.ErrorMessage, type: metadata.FailureDetails?.ErrorType),
            OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending => AcceptedAtFunction($"{nameof(GetOperation)}_{nameof(HttpStart)}", new { instanceId }, null),
            _ => Ok()
        };
    }

    [LoggerMessage(LogLevel.Information, "Instance state lookup returned no result. InstanceId: {InstanceId}")]
    private static partial void LogInstanceStateNotFound(ILogger logger, string instanceId);
}
