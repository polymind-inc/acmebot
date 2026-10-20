using System.Security.Claims;

using Acmebot.App.Functions.Http;
using Acmebot.App.Options;
using Acmebot.App.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace Acmebot.App.Tests;

[Collection(AppServiceAuthenticationCollection.Name)]
public sealed class HttpAuthenticationTests
{
    [Theory]
    [InlineData(nameof(AddCertificate))]
    [InlineData(nameof(RenewCertificate))]
    [InlineData(nameof(RevokeCertificate))]
    public async Task HttpFunctions_WhenAuthenticatedWithoutRequiredRole_ReturnForbidden(string functionName)
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("aad")) };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var roles = new AppRoleService(Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            Contacts = "admin@example.com",
            Endpoint = new Uri("https://ca.example/directory"),
            VaultBaseUrl = "https://vault.example/",
            RequireAppRoles = true
        }));

        var result = functionName switch
        {
            nameof(AddCertificate) => await new AddCertificate(accessor, roles, null!).HttpStart(context.Request, null!, null!),
            nameof(RenewCertificate) => await new RenewCertificate(accessor, roles, null!, null!).HttpStart(context.Request, "certificate", null!),
            nameof(RevokeCertificate) => await new RevokeCertificate(accessor, roles, null!, null!).HttpStart(context.Request, "certificate"),
            _ => throw new ArgumentOutOfRangeException(nameof(functionName))
        };

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    public static IEnumerable<object?[]> RejectedRequests()
    {
        string[] functions = [nameof(AddCertificate), nameof(GetAccount), nameof(GetCertificateRenewals), nameof(GetCertificates), nameof(GetConfiguration), nameof(GetDnsZones), nameof(GetOperation), nameof(RenewCertificate), nameof(RevokeCertificate), nameof(StaticPage)];

        foreach (var function in functions)
        {
            // An authenticated principal can be forged when the identity headers are untrusted.
            yield return [function, null, true];
            yield return [function, "false", true];
            yield return [function, "invalid", true];
            yield return [function, "true", false];
        }
    }

    [Theory]
    [MemberData(nameof(RejectedRequests))]
    public async Task HttpFunctions_WithoutTrustedAuthentication_RejectBeforeUsingServices(string functionName, string? authenticationSetting, bool authenticated)
    {
        using var authentication = new AppServiceAuthenticationScope(authenticationSetting);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("roles", "Acmebot.IssueCertificate"),
                new Claim("roles", "Acmebot.RevokeCertificate")
            ], authenticated ? "aad" : null))
        };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var options = Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            Contacts = "admin@example.com",
            Endpoint = new Uri("https://ca.example/directory"),
            VaultBaseUrl = "https://vault.example/"
        });

        // Null dependencies make any access to certificate, DNS, ACME or orchestration services fail.
        var result = functionName switch
        {
            nameof(AddCertificate) => await new AddCertificate(accessor, null!, null!).HttpStart(context.Request, null!, null!),
            nameof(GetAccount) => await new GetAccount(accessor, null!, options).HttpStart(context.Request),
            nameof(GetCertificateRenewals) => await new GetCertificateRenewals(accessor, null!, null!).HttpStart(context.Request, null!),
            nameof(GetCertificates) => await new GetCertificates(accessor, null!, null!).HttpStart(context.Request),
            nameof(GetConfiguration) => new GetConfiguration(accessor, null!).HttpStart(context.Request),
            nameof(GetDnsZones) => await new GetDnsZones(accessor, null!, null!).HttpStart(context.Request),
            nameof(GetOperation) => await new GetOperation(accessor, null!).HttpStart(context.Request, "operation", null!),
            nameof(RenewCertificate) => await new RenewCertificate(accessor, null!, null!, null!).HttpStart(context.Request, "certificate", null!),
            nameof(RevokeCertificate) => await new RevokeCertificate(accessor, null!, null!, null!).HttpStart(context.Request, "certificate"),
            nameof(StaticPage) => new StaticPage(accessor).Serve(context.Request),
            _ => throw new ArgumentOutOfRangeException(nameof(functionName))
        };

        Assert.IsType<UnauthorizedResult>(result);
    }
}
