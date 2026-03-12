using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Acmebot.App.Functions;

public class StaticPage(IHttpContextAccessor httpContextAccessor) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(StaticPage)}_{nameof(Serve)}")]
    public IActionResult Serve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest req)
    {
        if (!IsAuthenticationEnabled || !(User.Identity?.IsAuthenticated ?? false))
        {
            return Unauthorized();
        }

        return LocalStaticApp();
    }
}
