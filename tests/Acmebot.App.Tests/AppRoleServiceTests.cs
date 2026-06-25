using System.Security.Claims;

using Acmebot.App.Options;
using Acmebot.App.Services;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AppRoleServiceTests
{
    [Fact]
    public void HasRoles_WhenAppRolesAreNotRequired_ReturnsTrue()
    {
        var service = CreateService(requireAppRoles: false);
        var principal = CreatePrincipal();

        Assert.True(service.HasIssueCertificateRole(principal));
        Assert.True(service.HasRevokeCertificateRole(principal));
    }

    [Fact]
    public void HasIssueCertificateRole_WithIssueRole_ReturnsTrue()
    {
        var service = CreateService(requireAppRoles: true);
        var principal = CreatePrincipal(new Claim("roles", "Acmebot.IssueCertificate"));

        Assert.True(service.HasIssueCertificateRole(principal));
        Assert.False(service.HasRevokeCertificateRole(principal));
    }

    [Fact]
    public void HasRevokeCertificateRole_WithRevokeRole_ReturnsTrue()
    {
        var service = CreateService(requireAppRoles: true);
        var principal = CreatePrincipal(new Claim("roles", "Acmebot.RevokeCertificate"));

        Assert.False(service.HasIssueCertificateRole(principal));
        Assert.True(service.HasRevokeCertificateRole(principal));
    }

    [Fact]
    public void HasRoles_WithDifferentClaimType_ReturnsFalse()
    {
        var service = CreateService(requireAppRoles: true);
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Acmebot.IssueCertificate"));

        Assert.False(service.HasIssueCertificateRole(principal));
    }

    private static AppRoleService CreateService(bool requireAppRoles)
    {
        return new AppRoleService(Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            RequireAppRoles = requireAppRoles,
            VaultBaseUrl = "https://vault.example.net/",
            Contacts = "admin@example.com",
            Endpoint = new Uri("https://acme.example.net/directory")
        }));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
