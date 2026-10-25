using System.Net;
using System.Net.Http.Json;

using Azure.Core;

using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class IssueCertificateCommandTests
{
    [Theory]
    [InlineData(false, "https://acmebot.example/api/certificates")]
    [InlineData(true, "https://acmebot.example/api/certificates?replaceExisting=true")]
    public async Task RunCommandAsync_WithCertificateIssue_SendsReplaceExistingOnlyWhenRequested(bool replaceExisting, string expectedRequestUri)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = new IssueHandler(HttpStatusCode.Accepted);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);
        var commandLine = CommandLine.Parse(CreateArguments(replaceExisting));
        var options = CliOptions.Create(commandLine);

        var exitCode = await CliApplication.RunCommandAsync(commandLine, options, client, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(new Uri(expectedRequestUri), Assert.Single(handler.RequestUris));
        Assert.Contains("Operation accepted.", output.ToString());
    }

    [Fact]
    public async Task RunCommandAsync_WithCertificateIssueConflict_SuggestsReplaceExisting()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = new IssueHandler(HttpStatusCode.Conflict);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);
        var commandLine = CommandLine.Parse(CreateArguments(replaceExisting: false));
        var options = CliOptions.Create(commandLine);

        var exception = await Assert.ThrowsAsync<AcmebotApiException>(() => CliApplication.RunCommandAsync(commandLine, options, client, output, error, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Contains("--replace-existing", exception.Message);
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task RunCommandAsync_WithCertificateIssueConflictAndReplaceExisting_ReportsServerProblem()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var handler = new IssueHandler(HttpStatusCode.Conflict);
        using var client = new AcmebotApiClient(new HttpClient(handler), new Uri("https://acmebot.example/"), new TestCredential(), ["api://acmebot/.default"]);
        var commandLine = CommandLine.Parse(CreateArguments(replaceExisting: true));
        var options = CliOptions.Create(commandLine);

        var exception = await Assert.ThrowsAsync<AcmebotApiException>(() => CliApplication.RunCommandAsync(commandLine, options, client, output, error, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.DoesNotContain("--replace-existing", exception.Message);
    }

    private static string[] CreateArguments(bool replaceExisting)
    {
        string[] arguments = ["--endpoint", "https://acmebot.example", "certificate", "issue", "--name", "example-com", "--dns-name", "example.com", "--dns-provider", "Azure DNS", "--no-wait"];

        return replaceExisting ? [.. arguments, "--replace-existing"] : arguments;
    }

    private sealed class IssueHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public List<Uri?> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri);

            if (statusCode == HttpStatusCode.Accepted)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri("/api/operations/abc", UriKind.Relative);

                return Task.FromResult(accepted);
            }

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(new { status = (int)statusCode, detail = "The certificate is not managed by Acmebot." })
            });
        }
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }
}
