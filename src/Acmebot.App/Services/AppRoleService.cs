using System.Security.Claims;

using Acmebot.App.Options;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Services;

public class AppRoleService(IOptions<AcmebotOptions> options)
{
    private const string IssueCertificateAppRole = "Acmebot.IssueCertificate";
    private const string RevokeCertificateAppRole = "Acmebot.RevokeCertificate";

    public bool HasIssueCertificateRole(ClaimsPrincipal claimsPrincipal) =>
        !options.Value.RequireAppRoles || IsInAppRole(claimsPrincipal, IssueCertificateAppRole);

    public bool HasRevokeCertificateRole(ClaimsPrincipal claimsPrincipal) =>
        !options.Value.RequireAppRoles || IsInAppRole(claimsPrincipal, RevokeCertificateAppRole);

    private static bool IsInAppRole(ClaimsPrincipal claimsPrincipal, string role) =>
        claimsPrincipal.Claims.Where(x => x.Type == "roles").Any(x => x.Value == role);
}
