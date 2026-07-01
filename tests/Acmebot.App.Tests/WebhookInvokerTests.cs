using System.Net;

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

    private static WebhookInvoker CreateInvoker(HttpClient httpClient, IWebhookPayloadBuilder payloadBuilder)
    {
        return new WebhookInvoker(
            payloadBuilder,
            new StubHttpClientFactory(httpClient),
            Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
            {
                Contacts = "admin@example.com",
                Endpoint = new Uri("https://acme.example/directory"),
                VaultBaseUrl = "https://vault.example/",
                Webhook = new Uri("https://webhook.example/")
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
