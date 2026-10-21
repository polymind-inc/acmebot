using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Functions.Http;
using Acmebot.App.Options;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class GetAccountTests
{
    private static readonly Uri s_directoryUrl = new("https://acme.example/directory");
    private static readonly Uri s_accountUrl = new("https://acme.example/acct/123");

    [Fact]
    public void CreateAccountItem_ProjectsDirectoryMetadataWithoutAccountUri()
    {
        using var context = CreateAcmeClientContext(["authority.example", "ca.example.net"]);

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.Equal(s_directoryUrl, result.DirectoryUrl);
        Assert.Equal(["authority.example", "ca.example.net"], result.CaaIdentities);

        var response = System.Text.Json.JsonSerializer.SerializeToElement(result);
        Assert.False(response.TryGetProperty("accountUri", out _));
        Assert.DoesNotContain(s_accountUrl.OriginalString, response.GetRawText());
    }

    [Fact]
    public void CreateAccountItem_WithoutCaaIdentities_ReturnsEmptyCollection()
    {
        using var context = CreateAcmeClientContext([]);

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.Empty(result.CaaIdentities);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("ca.example", null, false)]
    [InlineData(null, "https://ca.example/hash/", false)]
    [InlineData("CA.EXAMPLE.", "https://ca.example/hash/", false)]
    [InlineData("ca.example", "/relative/", false)]
    [InlineData("ca.example", "https://ca.example/hash/", true)]
    public void CreateAccountItem_ReportsValidatedPreProvisioningMetadata(string? issuer, string? prefix, bool available)
    {
        using var context = CreateAcmeClientContext([], new AcmeDirectoryMetadata
        {
            IssuerDomainNames = issuer is null ? [] : [issuer],
            AccountHashPrefix = prefix
        });

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.Empty(result.CaaIdentities);
        Assert.Equal(available, result.DnsPersist01.PreProvisioningMetadataAvailable);
        Assert.Equal(issuer is null ? Array.Empty<string>() : [issuer], result.DnsPersist01.IssuerDomainNames);
        Assert.Equal(prefix, result.DnsPersist01.AccountHashPrefix);
    }

    [Fact]
    public void CreateAccountItem_WithOnlyCaaIdentities_DoesNotInferPreProvisioningSupport()
    {
        using var context = CreateAcmeClientContext(["ca.example"]);

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.False(result.DnsPersist01.PreProvisioningMetadataAvailable);
        Assert.Empty(result.DnsPersist01.IssuerDomainNames);
        Assert.Null(result.DnsPersist01.AccountHashPrefix);
    }

    [Fact]
    public void CreateAccountItem_WithoutDirectoryMetadata_ReturnsUnavailableMetadata()
    {
        using var context = CreateAcmeClientContext([], omitMetadata: true);

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.False(result.DnsPersist01.PreProvisioningMetadataAvailable);
        Assert.Empty(result.DnsPersist01.IssuerDomainNames);
        Assert.Null(result.DnsPersist01.AccountHashPrefix);
    }

    [Fact]
    public async Task HttpStart_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var httpContext = new DefaultHttpContext();
        var function = new GetAccount(
            new HttpContextAccessor { HttpContext = httpContext },
            acmeClientFactory: null!,
            Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
            {
                Contacts = "admin@example.com",
                Endpoint = s_directoryUrl,
                VaultBaseUrl = "https://vault.example/"
            }));

        var result = await function.HttpStart(httpContext.Request);

        Assert.IsType<UnauthorizedResult>(result);
    }

    private static AcmeClientContext CreateAcmeClientContext(IReadOnlyList<string> caaIdentities, AcmeDirectoryMetadata? metadata = null, bool omitMetadata = false)
    {
        var signer = AcmeSigner.CreateP256();

        return new AcmeClientContext
        {
            Client = new AcmeClient(s_directoryUrl),
            Directory = new AcmeDirectoryResource
            {
                NewNonce = new Uri("https://acme.example/new-nonce"),
                NewAccount = new Uri("https://acme.example/new-account"),
                NewOrder = new Uri("https://acme.example/new-order"),
                Metadata = omitMetadata ? null : metadata ?? new AcmeDirectoryMetadata
                {
                    CaaIdentities = caaIdentities
                }
            },
            Signer = signer,
            Account = new AcmeAccountHandle
            {
                AccountUrl = s_accountUrl,
                Signer = signer,
                Account = new AcmeAccountResource
                {
                    Status = AcmeAccountStatuses.Valid
                }
            }
        };
    }
}
