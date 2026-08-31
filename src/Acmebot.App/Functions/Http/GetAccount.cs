using Acmebot.App.Acme;
using Acmebot.App.Models;
using Acmebot.App.Options;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Http;

public class GetAccount(
    IHttpContextAccessor httpContextAccessor,
    AcmeClientFactory acmeClientFactory,
    IOptions<AcmebotOptions> options) : HttpFunctionBase(httpContextAccessor)
{
    private readonly Uri _directoryUrl = options.Value.Endpoint;

    [Function($"{nameof(GetAccount)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/account")] HttpRequest req)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var acmeContext = await acmeClientFactory.CreateClientAsync();

        return Ok(CreateAccountItem(acmeContext, _directoryUrl));
    }

    internal static AccountItem CreateAccountItem(AcmeClientContext acmeContext, Uri directoryUrl)
    {
        ArgumentNullException.ThrowIfNull(acmeContext);
        ArgumentNullException.ThrowIfNull(directoryUrl);

        return new AccountItem
        {
            AccountUri = acmeContext.Account.AccountUrl,
            DirectoryUrl = directoryUrl,
            CaaIdentities = acmeContext.Directory.Metadata?.CaaIdentities ?? []
        };
    }
}
