using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Functions.Orchestration;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AcmeOrderActivitiesTests
{
    [Fact]
    public async Task CreateOrderAsync_WithAlreadyReplacedProblem_RetriesWithoutReplaces()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newNonceUrl = new Uri("https://example.com/acme/new-nonce");
        var newOrderUrl = new Uri("https://example.com/acme/new-order");
        var orderUrl = new Uri("https://example.com/acme/order/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        handler.Enqueue(_ => CreateJsonResponse(HttpStatusCode.OK, new
        {
            newNonce = newNonceUrl,
            newAccount = new Uri("https://example.com/acme/new-account"),
            newOrder = newOrderUrl,
            renewalInfo = new Uri("https://example.com/acme/renewal-info")
        }));
        var directory = await client.GetDirectoryAsync(TestContext.Current.CancellationToken);
        handler.Enqueue(_ => CreateResponse(HttpStatusCode.OK, string.Empty, contentType: null, replayNonce: "bm9uY2Ux"));
        handler.Enqueue(_ => CreateJsonResponse(
            HttpStatusCode.BadRequest,
            new
            {
                type = AcmeProblemTypes.AlreadyReplaced.Value,
                detail = "already replaced"
            },
            replayNonce: "bm9uY2Uy",
            contentType: "application/problem+json"));
        handler.Enqueue(_ => CreateJsonResponse(
            HttpStatusCode.Created,
            new
            {
                status = "pending",
                authorizations = new[] { "https://example.com/acme/authz/1" },
                finalize = "https://example.com/acme/finalize/1"
            },
            replayNonce: "bm9uY2Uz",
            location: orderUrl));
        var context = new AcmeClientContext
        {
            Client = client,
            Directory = directory,
            Signer = signer,
            Account = CreateAccountHandle(signer)
        };

        var result = await AcmeOrderActivities.CreateOrderAsync(
            context,
            ["example.com"],
            profile: "tlsserver",
            replaces: "old-cert-id",
            NullLogger<AcmeOrderActivities>.Instance);

        var postRequests = handler.Requests.Where(x => x.Method == HttpMethod.Post).ToArray();
        Assert.Equal(2, postRequests.Length);
        Assert.Equal(orderUrl, result.OrderUrl);
        Assert.Equal(AcmeOrderStatuses.Pending, result.Payload.Status);

        using var firstPayload = postRequests[0].GetPayloadJson();
        using var secondPayload = postRequests[1].GetPayloadJson();
        Assert.Equal("old-cert-id", firstPayload.RootElement.GetProperty("replaces").GetString());
        Assert.Equal("tlsserver", firstPayload.RootElement.GetProperty("profile").GetString());
        Assert.False(secondPayload.RootElement.TryGetProperty("replaces", out _));
        Assert.Equal("tlsserver", secondPayload.RootElement.GetProperty("profile").GetString());
    }

    private static AcmeAccountHandle CreateAccountHandle(AcmeSigner signer)
    {
        return new AcmeAccountHandle
        {
            AccountUrl = new Uri("https://example.com/acme/account/1"),
            Signer = signer,
            Account = new AcmeAccountResource
            {
                Status = AcmeAccountStatuses.Valid
            }
        };
    }

    private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload, string? replayNonce = null, Uri? location = null, string contentType = "application/json")
    {
        return CreateResponse(statusCode, JsonSerializer.Serialize(payload), contentType, replayNonce, location);
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, string? contentType, string? replayNonce = null, Uri? location = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.ASCII)
        };

        response.Content.Headers.ContentType = contentType is null ? null : new MediaTypeHeaderValue(contentType);
        response.Headers.Location = location;

        if (replayNonce is not null)
        {
            response.Headers.TryAddWithoutValidation("Replay-Nonce", replayNonce);
        }

        return response;
    }

    private static string DecodeBase64UrlUtf8(string value) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public List<RecordedRequest> Requests { get; } = [];

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) => _responses.Enqueue(responseFactory);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await RecordedRequest.CreateAsync(request, cancellationToken));

            return _responses.TryDequeue(out var responseFactory)
                ? responseFactory(request)
                : throw new InvalidOperationException("No response was configured for the HTTP request.");
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri? RequestUri, string? Content)
    {
        public AcmeSignedMessage GetSignedMessage()
        {
            return JsonSerializer.Deserialize<AcmeSignedMessage>(Content!)
                ?? throw new InvalidOperationException("The request body did not contain a signed ACME message.");
        }

        public JsonDocument GetPayloadJson() => JsonDocument.Parse(DecodeBase64UrlUtf8(GetSignedMessage().Payload));

        public static async Task<RecordedRequest> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        }
    }
}
