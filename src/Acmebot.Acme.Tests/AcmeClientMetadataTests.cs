using System.Net;
using System.Net.Http.Headers;

using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeClientMetadataTests
{
    [Fact]
    public async Task CreateOrderAsync_PropagatesLocationRetryAfterAndLinks()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var orderUrl = new Uri("https://example.com/acme/order/1");
        var alternateOrderUrl = new Uri("https://example.com/acme/order/alternate");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ =>
        {
            var response = AcmeTestSupport.CreateJsonResponse(
                HttpStatusCode.Created,
                new { status = "pending" },
                replayNonce: "bm9uY2Uy",
                location: orderUrl);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            response.Headers.TryAddWithoutValidation("Link", $"<{alternateOrderUrl}>;rel=\"alternate\";title=\"alternate order\"");
            return response;
        });

        var result = await client.CreateOrderAsync(account, new AcmeNewOrderRequest
        {
            Identifiers = [new AcmeIdentifier { Type = AcmeIdentifierTypes.Dns, Value = "example.com" }]
        }, TestContext.Current.CancellationToken);

        Assert.Equal(orderUrl, result.Location);
        Assert.Equal(TimeSpan.FromSeconds(30), result.RetryAfter);
        var link = Assert.Single(result.Links);
        Assert.Equal(alternateOrderUrl, link.Uri);
        Assert.Equal("alternate", link.Relation);
        Assert.Equal("alternate order", link.Title);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsProtocolExceptionWhenProblemDetailsIncludeRetryAfterAndLinks()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newOrderUrl = new Uri("https://example.com/acme/new-order");
        var documentationUrl = new Uri("https://example.com/docs/rate-limit");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler, newOrder: newOrderUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ =>
        {
            var response = AcmeTestSupport.CreateJsonResponse(
                HttpStatusCode.TooManyRequests,
                new
                {
                    type = AcmeProblemTypes.RateLimited.Value,
                    detail = "too many orders",
                    status = 429
                },
                replayNonce: "bm9uY2Uy",
                contentType: "application/problem+json");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            response.Headers.TryAddWithoutValidation("Link", $"<{documentationUrl}>;rel=\"help\";type=\"text/html\"");
            return response;
        });

        var exception = await Assert.ThrowsAsync<AcmeProtocolException>(() => client.CreateOrderAsync(account, new AcmeNewOrderRequest
        {
            Identifiers = [new AcmeIdentifier { Type = AcmeIdentifierTypes.Dns, Value = "example.com" }]
        }, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(newOrderUrl, exception.RequestUri);
        Assert.Equal("bm9uY2Uy", exception.ReplayNonce);
        Assert.Equal(TimeSpan.FromSeconds(120), exception.RetryAfter);
        Assert.Equal("too many orders", exception.Message);
        Assert.NotNull(exception.Problem);
        Assert.Equal(AcmeProblemTypes.RateLimited, exception.Problem!.Type);
        Assert.Equal(429, exception.Problem.Status);
        var link = Assert.Single(exception.Links);
        Assert.Equal(documentationUrl, link.Uri);
        Assert.Equal("help", link.Relation);
        Assert.Equal("text/html", link.MediaType);
    }
}
