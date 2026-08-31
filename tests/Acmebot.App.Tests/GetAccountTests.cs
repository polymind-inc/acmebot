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
    public void CreateAccountItem_ProjectsAccountAndDirectoryMetadata()
    {
        using var context = CreateAcmeClientContext(["authority.example", "ca.example.net"]);

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.Equal(s_accountUrl, result.AccountUri);
        Assert.Equal(s_directoryUrl, result.DirectoryUrl);
        Assert.Equal(["authority.example", "ca.example.net"], result.CaaIdentities);
    }

    [Fact]
    public void CreateAccountItem_WithoutCaaIdentities_ReturnsEmptyCollection()
    {
        using var context = CreateAcmeClientContext([]);

        var result = GetAccount.CreateAccountItem(context, s_directoryUrl);

        Assert.Empty(result.CaaIdentities);
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

    private static AcmeClientContext CreateAcmeClientContext(IReadOnlyList<string> caaIdentities)
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
                Metadata = new AcmeDirectoryMetadata
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
