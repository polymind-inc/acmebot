using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Http;

public partial class RevokeCertificate(
    IHttpContextAccessor httpContextAccessor,
    AppRoleService appRoleService,
    CertificateOperationService certificateOperationService,
    ILogger<RevokeCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(RevokeCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificates/{certificateName}/revoke")] HttpRequest req,
        string certificateName)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (!appRoleService.HasRevokeCertificateRole(User))
        {
            return Forbid();
        }

        await certificateOperationService.RevokeCertificateAsync(certificateName, req.HttpContext.RequestAborted);

        LogCertificateRevoked(logger, certificateName);

        return Ok();
    }

    [LoggerMessage(LogLevel.Information, "Certificate revoked. CertificateName: {CertificateName}")]
    private static partial void LogCertificateRevoked(ILogger logger, string certificateName);
}
