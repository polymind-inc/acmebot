using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Azure.Core;

using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class AccountCommandTests
{
    [Theory]
    [InlineData("table", true)]
    [InlineData("table", false)]
    [InlineData("json", true)]
    [InlineData("json", false)]
    public async Task RunCommandAsync_WithAccountShow_PrintsAuthenticatedApiResponse(string format, bool hasIdentities)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = new AccountHandler(hasIdentities);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);
        var commandLine = CommandLine.Parse(["--endpoint", "https://acmebot.example", "ACCOUNT", "SHOW", "--format", format]);
        var options = CliOptions.Create(commandLine);

        var exitCode = await CliApplication.RunCommandAsync(commandLine, options, client, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("", error.ToString());
        Assert.Equal(1, handler.RequestCount);

        if (format == "json")
        {
            using var document = JsonDocument.Parse(output.ToString());
            Assert.Equal(3, document.RootElement.EnumerateObject().Count());
            Assert.False(document.RootElement.TryGetProperty("accountUri", out _));
            Assert.Equal("https://ca.example/directory", document.RootElement.GetProperty("directoryUrl").GetString());
            Assert.Equal(hasIdentities ? ["ca.example", "other.example"] : Array.Empty<string>(),
                document.RootElement.GetProperty("caaIdentities").EnumerateArray().Select(value => value.GetString()));
            var metadata = document.RootElement.GetProperty("dnsPersist01");
            Assert.Equal(hasIdentities, metadata.GetProperty("preProvisioningMetadataAvailable").GetBoolean());
            Assert.Equal(hasIdentities ? ["persist.example"] : Array.Empty<string>(),
                metadata.GetProperty("issuerDomainNames").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(hasIdentities ? "https://ca.example/hash/" : null, metadata.GetProperty("accountHashPrefix").GetString());
        }
        else
        {
            Assert.Equal(
                $"Directory URL: https://ca.example/directory{Environment.NewLine}" +
                $"CAA Identities: {(hasIdentities ? "ca.example, other.example" : "<none>")}{Environment.NewLine}" +
                $"DNS-PERSIST-01 Pre-provisioning Metadata: {(hasIdentities ? "Available" : "Not available")}{Environment.NewLine}" +
                $"DNS-PERSIST-01 Issuer Domain Names: {(hasIdentities ? "persist.example" : "<none>")}{Environment.NewLine}" +
                $"DNS-PERSIST-01 Account Hash Prefix: {(hasIdentities ? "https://ca.example/hash/" : "<none>")}{Environment.NewLine}" +
                $"DNS-PERSIST-01 issuance and record generation are not supported by Acmebot yet.{Environment.NewLine}",
                output.ToString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAccountAsync_PreservesDirectoryMetadata(bool hasIdentities)
    {
        using var handler = new AccountHandler(hasIdentities);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);

        var account = await client.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://ca.example/directory", account.DirectoryUrl.OriginalString);
        Assert.Equal(hasIdentities ? ["ca.example", "other.example"] : Array.Empty<string>(), account.CaaIdentities);
        Assert.NotNull(account.DnsPersist01);
        Assert.Equal(hasIdentities, account.DnsPersist01.PreProvisioningMetadataAvailable);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("table")]
    [InlineData("json")]
    public async Task RunCommandAsync_WithOlderServer_OmitsAccountUri(string format)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = new AccountHandler(true, omitMetadata: true, includeAccountUri: true);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);
        var commandLine = CommandLine.Parse(["--endpoint", "https://acmebot.example", "account", "show", "--format", format]);

        var exitCode = await CliApplication.RunCommandAsync(commandLine, CliOptions.Create(commandLine), client, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("accountUri", output.ToString());
        Assert.DoesNotContain("https://ca.example/acct/123", output.ToString());
        if (format == "table")
        {
            Assert.Contains("Unknown (server did not report metadata)", output.ToString());
        }
        else
        {
            using var response = JsonDocument.Parse(output.ToString());
            Assert.False(response.RootElement.TryGetProperty("accountUri", out _));
        }
        Assert.DoesNotContain("Pre-provisioning Metadata: Available", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [InlineData(new string[] { "account" }, "Missing account subcommand.")]
    [InlineData(new string[] { "account", "unknown" }, "Unknown account subcommand 'unknown'.")]
    [InlineData(new string[] { "account", "show", "extra" }, "Unexpected argument 'extra'.")]
    public async Task RunAsync_WithInvalidAccountCommand_ReturnsUsage(string[] arguments, string message)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--endpoint", "https://acmebot.example", .. arguments], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains(message, error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithHelp_ListsAccountCommand()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--help"], output, error, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("acmebot account show [options]", output.ToString());
    }

    private sealed class AccountHandler(bool hasIdentities, bool omitMetadata = false, bool includeAccountUri = false) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri("https://acmebot.example/api/account"), request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("token", request.Headers.Authorization?.Parameter);
            Assert.Contains(request.Headers.Accept, value => value.MediaType == "application/json");

            var response = new Dictionary<string, object?>
            {
                ["directoryUrl"] = "https://ca.example/directory",
                ["caaIdentities"] = hasIdentities ? new[] { "ca.example", "other.example" } : []
            };
            if (includeAccountUri)
            {
                response["accountUri"] = "https://ca.example/acct/123";
            }
            if (!omitMetadata)
            {
                response["dnsPersist01"] = new
                {
                    preProvisioningMetadataAvailable = hasIdentities,
                    issuerDomainNames = hasIdentities ? new[] { "persist.example" } : [],
                    accountHashPrefix = hasIdentities ? "https://ca.example/hash/" : null
                };
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
        }
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }
}
