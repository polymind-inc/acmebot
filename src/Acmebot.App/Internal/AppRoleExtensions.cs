using System.Security.Claims;

namespace Acmebot.App.Internal;

internal static class AppRoleExtensions
{
    private const string IssueCertificateAppRole = "Acmebot.IssueCertificate";
    private const string RevokeCertificateAppRole = "Acmebot.RevokeCertificate";

    public static bool HasIssueCertificateRole(this ClaimsPrincipal claimsPrincipal) =>
        !IsAppRoleRequired || claimsPrincipal.IsInAppRole(IssueCertificateAppRole);

    public static bool HasRevokeCertificateRole(this ClaimsPrincipal claimsPrincipal) =>
        !IsAppRoleRequired || claimsPrincipal.IsInAppRole(RevokeCertificateAppRole);

    private static bool IsAppRoleRequired => bool.TryParse(Environment.GetEnvironmentVariable("Acmebot:AppRoleRequired"), out var result) && result;

    private static bool IsInAppRole(this ClaimsPrincipal claimsPrincipal, string role) =>
        claimsPrincipal.Claims.Where(x => x.Type == "roles").Any(x => x.Value == role);
}
