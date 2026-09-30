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
            Assert.Equal("https://ca.example/acct/123", document.RootElement.GetProperty("accountUri").GetString());
            Assert.Equal("https://ca.example/directory", document.RootElement.GetProperty("directoryUrl").GetString());
            Assert.Equal(hasIdentities ? ["ca.example", "other.example"] : Array.Empty<string>(),
                document.RootElement.GetProperty("caaIdentities").EnumerateArray().Select(value => value.GetString()));
        }
        else
        {
            Assert.Equal(
                $"Account URI: https://ca.example/acct/123{Environment.NewLine}" +
                $"Directory URL: https://ca.example/directory{Environment.NewLine}" +
                $"CAA Identities: {(hasIdentities ? "ca.example, other.example" : "<none>")}{Environment.NewLine}",
                output.ToString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAccountAsync_PreservesAccountAndDirectoryMetadata(bool hasIdentities)
    {
        using var handler = new AccountHandler(hasIdentities);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);

        var account = await client.GetAccountAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://ca.example/acct/123", account.AccountUri.OriginalString);
        Assert.Equal("https://ca.example/directory", account.DirectoryUrl.OriginalString);
        Assert.Equal(hasIdentities ? ["ca.example", "other.example"] : Array.Empty<string>(), account.CaaIdentities);
        Assert.Equal(1, handler.RequestCount);
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

    private sealed class AccountHandler(bool hasIdentities) : HttpMessageHandler
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

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    accountUri = "https://ca.example/acct/123",
                    directoryUrl = "https://ca.example/directory",
                    caaIdentities = hasIdentities ? new[] { "ca.example", "other.example" } : []
                })
            });
        }
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }
}
