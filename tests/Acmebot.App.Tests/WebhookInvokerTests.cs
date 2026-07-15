using System.Net;
using System.Text;
using System.Text.Json;

using Acmebot.App.Notifications;
using Acmebot.App.Options;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class WebhookInvokerTests
{
    [Fact]
    public async Task SendCompletedEventAsync_WithFailureResponse_DoesNotThrow()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("temporary failure")
        }));
        var invoker = CreateInvoker(httpClient, new StubWebhookPayloadBuilder());

        await invoker.SendCompletedEventAsync(
            "example-com",
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            ["example.com"],
            "acme.example");
    }

    [Fact]
    public async Task SendCompletedEventAsync_WhenPostThrows_DoesNotThrow()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("network unavailable")));
        var invoker = CreateInvoker(httpClient, new StubWebhookPayloadBuilder());

        await invoker.SendCompletedEventAsync(
            "example-com",
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            ["example.com"],
            "acme.example");
    }

    [Fact]
    public async Task SendFailedEventAsync_WhenPayloadBuilderThrows_DoesNotThrow()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var invoker = CreateInvoker(httpClient, new ThrowingWebhookPayloadBuilder());

        await invoker.SendFailedEventAsync("example-com", ["example.com"]);
    }

    [Fact]
    public async Task SendCompletedEventAsync_PostsJsonBody()
    {
        using var handler = new CapturingHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var invoker = CreateInvoker(httpClient, new StubWebhookPayloadBuilder());

        await invoker.SendCompletedEventAsync(
            "example-com",
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            ["example.com"],
            "acme.example");

        Assert.Equal("application/json", handler.ContentType);
        Assert.NotNull(handler.Body);
        Assert.Equal(Encoding.UTF8.GetByteCount(handler.Body), handler.ContentLength);

        using var body = JsonDocument.Parse(handler.Body);
        Assert.Equal("example-com", body.RootElement.GetProperty("certificateName").GetString());
    }

    [Fact]
    public async Task SendCompletedEventAsync_WhenCompletedEventDisabled_DoesNotPost()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var invoker = CreateInvoker(httpClient, new ThrowingWebhookPayloadBuilder(), WebhookEvents.Failed);

        await invoker.SendCompletedEventAsync(
            "example-com",
            DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            ["example.com"],
            "acme.example");

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task SendFailedEventAsync_WhenOnlyFailedEventEnabled_Posts()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var invoker = CreateInvoker(httpClient, new StubWebhookPayloadBuilder(), WebhookEvents.Failed);

        await invoker.SendFailedEventAsync("example-com", ["example.com"]);

        Assert.Equal(1, requestCount);
    }

    private static WebhookInvoker CreateInvoker(HttpClient httpClient, IWebhookPayloadBuilder payloadBuilder, WebhookEvents events = WebhookEvents.All)
    {
        return new WebhookInvoker(
            payloadBuilder,
            new StubHttpClientFactory(httpClient),
            Microsoft.Extensions.Options.Options.Create(new WebhookOptions
            {
                Endpoint = new Uri("https://webhook.example/"),
                Events = events
            }),
            NullLogger<WebhookInvoker>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string? ContentType { get; private set; }

        public long? ContentLength { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ContentLength = request.Content!.Headers.ContentLength;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content.Headers.ContentType?.MediaType;

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StubWebhookPayloadBuilder : IWebhookPayloadBuilder
    {
        public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
        {
            return new
            {
                certificateName,
                expirationDate,
                dnsNames,
                acmeEndpoint
            };
        }

        public object BuildFailed(string certificateName, IEnumerable<string> dnsNames)
        {
            return new
            {
                certificateName,
                dnsNames
            };
        }
    }

    private sealed class ThrowingWebhookPayloadBuilder : IWebhookPayloadBuilder
    {
        public object BuildCompleted(string certificateName, DateTimeOffset? expirationDate, IEnumerable<string> dnsNames, string acmeEndpoint)
        {
            throw new InvalidOperationException("payload failed");
        }

        public object BuildFailed(string certificateName, IEnumerable<string> dnsNames)
        {
            throw new InvalidOperationException("payload failed");
        }
    }
}
