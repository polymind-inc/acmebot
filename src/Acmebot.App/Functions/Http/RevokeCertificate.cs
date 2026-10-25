using Acmebot.App.Extensions;
using Acmebot.App.Options;
using Acmebot.App.Services;

using Azure.Functions.Worker.Extensions.HttpApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Http;

public partial class RevokeCertificate(
    IHttpContextAccessor httpContextAccessor,
    AppRoleService appRoleService,
    CertificateOperationService certificateOperationService,
    IOptions<AcmebotOptions> options,
    ILogger<RevokeCertificate> logger) : HttpFunctionBase(httpContextAccessor)
{
    [Function($"{nameof(RevokeCertificate)}_{nameof(HttpStart)}")]
    public async Task<IActionResult> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/certificates/{certificateName}/revoke")] HttpRequest req,
        string certificateName)
    {
        if (!IsAuthenticationEnabled || User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        if (!appRoleService.HasRevokeCertificateRole(User))
        {
            return Forbid();
        }

        var certificate = await certificateOperationService.FindCertificateAsync(certificateName, req.HttpContext.RequestAborted);

        if (certificate is null)
        {
            return Problem($"The certificate '{certificateName}' was not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // Only revoke certificates this Acmebot issued through its current ACME endpoint. Anything else
        // belongs to another issuer or account and must be revoked there.
        if (!certificate.Properties.IsIssuedByAcmebot() || !certificate.Properties.IsSameEndpoint(options.Value.Endpoint))
        {
            return Problem(
                $"The certificate '{certificateName}' was not issued by this Acmebot ACME endpoint and cannot be revoked.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Certificate is not managed by this Acmebot endpoint");
        }

        await certificateOperationService.RevokeCertificateAsync(certificate, req.HttpContext.RequestAborted);

        LogCertificateRevoked(logger, certificateName);

        return Ok();
    }

    [LoggerMessage(LogLevel.Information, "Certificate revoked. CertificateName: {CertificateName}")]
    private static partial void LogCertificateRevoked(ILogger logger, string certificateName);
}
