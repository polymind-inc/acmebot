using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;

namespace Acmebot.App.Functions;

public class GetInstanceState(IHttpContextAccessor httpContextAccessor) : HttpFunctionBase(httpContextAccessor)
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
            return BadRequest();
        }

        return status.RuntimeStatus switch
        {
            OrchestrationRuntimeStatus.Failed => Problem(status.SerializedOutput),
            OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending => AcceptedAtFunction($"{nameof(GetInstanceState)}_{nameof(HttpStart)}", new { instanceId }, null),
            _ => Ok()
        };
    }
}
