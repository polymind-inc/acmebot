using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class GetDnsZones(
    IHttpContextAccessor httpContextAccessor,
    DnsZoneQueryService dnsZoneQueryService,
    ILogger<GetDnsZones> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetDnsZones)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/dns-zones")] HttpRequest req)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized();
        }

        var output = await dnsZoneQueryService.GetAllDnsZonesAsync(req.HttpContext.RequestAborted);

        LogDnsZonesRetrieved(logger, output.Count);

        return Ok(output);
    }

    [LoggerMessage(LogLevel.Information, "DNS zone list retrieved. Count: {Count}")]
    private static partial void LogDnsZonesRetrieved(ILogger logger, int count);
}
