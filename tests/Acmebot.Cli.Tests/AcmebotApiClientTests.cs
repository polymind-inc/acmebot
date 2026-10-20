using System.Net;
using System.Text;
using System.Text.Json;

using Azure.Core;

using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class AcmebotApiClientTests
{
    [Theory]
    [InlineData("http://acmebot.example/")]
    [InlineData("https://user:password@acmebot.example/")]
    public void Constructor_WithUnsafeEndpoint_RejectsBeforeAcquiringToken(string endpoint)
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var credential = new TestCredential("token");

        Assert.Throws<CliException>(() => new AcmebotApiClient(httpClient, new Uri(endpoint), credential, ["api://acmebot/.default"]));
        Assert.Empty(handler.Requests);
        Assert.Equal(0, credential.RequestCount);
    }

    [Theory]
    [InlineData("https://other.example/api/operations/abc")]
    [InlineData("http://acmebot.example/api/operations/abc")]
    [InlineData("https://acmebot.example:444/api/operations/abc")]
    [InlineData("//other.example/api/operations/abc")]
    [InlineData("https://acmebot.example@other.example/api/operations/abc")]
    [InlineData("https://user:password@acmebot.example/api/operations/abc")]
    public async Task RenewCertificateAsync_WithUnsafeLocation_DoesNotSendTokenToLocation(string location)
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var credential = new TestCredential("token");
        using var client = new AcmebotApiClient(httpClient, new Uri("https://acmebot.example/"), credential, ["api://acmebot/.default"]);
        handler.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        });

        var exception = await Assert.ThrowsAsync<AcmebotApiException>(() => client.RenewCertificateAsync("example", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Equal(new Uri("https://acmebot.example/api/certificates/example/renew"), Assert.Single(handler.Requests).RequestUri);
        Assert.Equal(1, credential.RequestCount);
    }

    [Theory]
    [InlineData("/api/operations/abc", "https://acmebot.example/api/operations/abc")]
    [InlineData("api/operations/abc", "https://acmebot.example/base/api/operations/abc")]
    [InlineData("https://ACMEBOT.example:443/api/operations/abc", "https://acmebot.example/api/operations/abc")]
    public async Task RenewCertificateAsync_WithSameOriginLocation_ResolvesAndPolls(string location, string expected)
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmebotApiClient(httpClient, new Uri("https://acmebot.example/base/"), new TestCredential("token"), ["api://acmebot/.default"]);
        handler.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        });
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var operationLocation = await client.RenewCertificateAsync("example", TestContext.Current.CancellationToken);
        await client.WaitForOperationAsync(operationLocation, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TextWriter.Null, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri(expected), operationLocation);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new Uri(expected), handler.Requests[1].RequestUri);
        Assert.Equal("token", handler.Requests[1].AuthorizationParameter);
    }

    [Theory]
    [InlineData("https://other.example/api/operations/abc")]
    [InlineData("http://acmebot.example/api/operations/abc")]
    [InlineData("https://acmebot.example:444/api/operations/abc")]
    [InlineData("api/operations/abc")]
    [InlineData("https://user:password@acmebot.example/api/operations/abc")]
    public async Task WaitForOperationAsync_WithUnsafeLocation_RejectsBeforeAcquiringToken(string location)
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var credential = new TestCredential("token");
        using var client = new AcmebotApiClient(httpClient, new Uri("https://acmebot.example/"), credential, ["api://acmebot/.default"]);

        await Assert.ThrowsAsync<AcmebotApiException>(() => client.WaitForOperationAsync(
            new Uri(location, UriKind.RelativeOrAbsolute), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TextWriter.Null, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
        Assert.Equal(0, credential.RequestCount);
    }

    [Fact]
    public async Task GetCertificatesAsync_SendsBearerTokenAndDeserializesResponse()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmebotApiClient(
            httpClient,
            new Uri("https://acmebot.example/"),
            new TestCredential("token"),
            ["api://acmebot/.default"]);

        handler.Enqueue(_ => CreateJsonResponse(HttpStatusCode.OK, new[]
        {
            new
            {
                id = "https://vault.example/certificates/example",
                name = "example",
                dnsNames = new[] { "example.com" },
                dnsProviderName = "Azure DNS",
                createdOn = "2026-06-01T00:00:00+00:00",
                expiresOn = "2026-09-01T00:00:00+00:00",
                enabled = true,
                isIssuedByAcmebot = true,
                isSameEndpoint = true
            }
        }));

        var certificates = await client.GetCertificatesAsync(TestContext.Current.CancellationToken);

        var certificate = Assert.Single(certificates);
        Assert.Equal("example", certificate.Name);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://acmebot.example/api/certificates"), request.RequestUri);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("token", request.AuthorizationParameter);
        Assert.Contains("application/json", request.Accept);
    }

    [Fact]
    public async Task IssueCertificateAsync_PostsPolicyAndReturnsOperationLocation()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmebotApiClient(
            httpClient,
            new Uri("https://acmebot.example/"),
            new TestCredential("token"),
            ["api://acmebot/.default"]);

        handler.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.Headers.Location = new Uri("/api/operations/abc", UriKind.Relative);

            return response;
        });

        var location = await client.IssueCertificateAsync(new CertificatePolicyItem
        {
            CertificateName = "example",
            DnsNames = ["example.com"],
            DnsProviderName = "Azure DNS",
            KeyType = "RSA",
            KeySize = 2048,
            Profile = "tlsserver"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://acmebot.example/api/operations/abc"), location);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://acmebot.example/api/certificates"), request.RequestUri);

        Assert.NotNull(request.Content);
        using var document = JsonDocument.Parse(request.Content);
        Assert.Equal("example", document.RootElement.GetProperty("certificateName").GetString());
        Assert.Equal("example.com", document.RootElement.GetProperty("dnsNames")[0].GetString());
        Assert.Equal("Azure DNS", document.RootElement.GetProperty("dnsProviderName").GetString());
        Assert.Equal("tlsserver", document.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public async Task WaitForOperationAsync_WithImmediateOk_DoesNotWaitBeforeFirstRequest()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await client.WaitForOperationAsync(
            new Uri("https://acmebot.example/api/operations/abc"),
            TimeSpan.FromDays(1),
            TimeSpan.FromMilliseconds(100),
            TextWriter.Null,
            TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://acmebot.example/api/operations/abc"), request.RequestUri);
    }

    [Fact]
    public async Task WaitForOperationAsync_WithAcceptedThenOk_PollsUntilComplete()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);
        using var progress = new StringWriter();

        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await client.WaitForOperationAsync(
            new Uri("https://acmebot.example/api/operations/abc"),
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(5),
            progress,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("Operation is still running...", progress.ToString());
    }

    [Fact]
    public async Task WaitForOperationAsync_WithFailureDuringPolling_ThrowsApiException()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        handler.Enqueue(_ => CreateJsonResponse(HttpStatusCode.BadRequest, new
        {
            title = "Invalid operation"
        }));

        var ex = await Assert.ThrowsAsync<AcmebotApiException>(() => client.WaitForOperationAsync(
            new Uri("https://acmebot.example/api/operations/abc"),
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(5),
            TextWriter.Null,
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Invalid operation", ex.Message);
    }

    [Fact]
    public async Task WaitForOperationAsync_WithTimeout_ThrowsTimeoutException()
    {
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = CreateClient(httpClient);

        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Accepted));

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => client.WaitForOperationAsync(
            new Uri("https://acmebot.example/api/operations/abc"),
            TimeSpan.FromDays(1),
            TimeSpan.FromMilliseconds(10),
            TextWriter.Null,
            TestContext.Current.CancellationToken));

        Assert.Contains("Operation did not complete within", ex.Message);
        Assert.Single(handler.Requests);
    }

    private static AcmebotApiClient CreateClient(HttpClient httpClient)
    {
        return new AcmebotApiClient(
            httpClient,
            new Uri("https://acmebot.example/"),
            new TestCredential("token"),
            ["api://acmebot/.default"]);
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, object content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
        };
    }

    private sealed class TestCredential(string token) : TokenCredential
    {
        public int RequestCount { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            RequestCount++;
            return new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = [];

        public List<RecordedRequest> Requests { get; } = [];

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response) => _responses.Enqueue(response);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.Accept.Select(value => value.MediaType ?? "").ToArray(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            return _responses.Dequeue()(request);
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        IReadOnlyList<string> Accept,
        string? Content);
}
