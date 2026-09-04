using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Acmebot.App.Functions.Http;

public class Ping(IHttpContextAccessor httpContextAccessor) : HttpFunctionBase(httpContextAccessor)
{
    // Anonymous and unauthenticated on purpose: this is the target for healthCheckPath, so the
    // Flex Consumption platform can invoke real worker code on every always ready instance
    // without ever going through App Service Authentication.
    [Function($"{nameof(Ping)}_{nameof(Get)}")]
    public IActionResult Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/ping")] HttpRequest req)
    {
        return Ok();
    }
}
