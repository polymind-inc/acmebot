using Acmebot.App.Options;
using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Http;

public class GetConfiguration(
    IHttpContextAccessor httpContextAccessor,
    IOptions<AcmebotOptions> options) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(GetConfiguration)}_{nameof(HttpStart)}")]
    public IActionResult HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/configuration")] HttpRequest req)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        req.HttpContext.Response.Headers.CacheControl = "no-store";

        return Ok(ConfigurationSnapshot.Create(options.Value));
    }
}
