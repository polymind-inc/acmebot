using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class GetCertificates(
    IHttpContextAccessor httpContextAccessor,
    CertificateQueryService certificateQueryService,
    ILogger<GetCertificates> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetCertificates)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/certificates")] HttpRequest req)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        var output = await certificateQueryService.GetAllCertificatesAsync(req.HttpContext.RequestAborted);

        LogCertificatesRetrieved(logger, output.Count);

        return Ok(output);
    }

    [LoggerMessage(LogLevel.Information, "Certificate list retrieved. Count: {Count}")]
    private static partial void LogCertificatesRetrieved(ILogger logger, int count);
}
