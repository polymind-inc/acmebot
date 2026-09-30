using System.Text.Json;

using Acmebot.App.Models;

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

        var status = ReadIssuanceStatus(metadata);

        return metadata.RuntimeStatus switch
        {
            OrchestrationRuntimeStatus.Failed => Problem(detail: BuildFailureDetail(metadata.FailureDetails?.ErrorMessage, status), type: metadata.FailureDetails?.ErrorType),
            OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending => Accepted(Url.RouteUrl($"{nameof(GetOperation)}_{nameof(HttpStart)}", new { instanceId }), status),
            _ => Ok()
        };
    }

    internal static string? BuildFailureDetail(string? errorMessage, CertificateIssuanceStatus? status)
    {
        return status is not null ? $"{errorMessage} (failed during: {status.Message})" : errorMessage;
    }

    internal static CertificateIssuanceStatus? ReadIssuanceStatus(OrchestrationMetadata metadata)
    {
        if (metadata.SerializedCustomStatus is null)
        {
            return null;
        }

        try
        {
            return metadata.ReadCustomStatusAs<CertificateIssuanceStatus>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(LogLevel.Information, "Instance state lookup returned no result. InstanceId: {InstanceId}")]
    private static partial void LogInstanceStateNotFound(ILogger logger, string instanceId);
}
