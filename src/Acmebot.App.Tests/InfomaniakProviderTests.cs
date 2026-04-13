using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

/// <summary>
/// Tests for InfomaniakProvider using a fake HTTP handler — no real domain required.
/// Each test enqueues mock JSON responses matching the Infomaniak REST API v1 format.
/// </summary>
public sealed class InfomaniakProviderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Builds a fake 200 response with an Infomaniak-style success envelope.</summary>
    private static HttpResponseMessage OkJson(object data)
    {
        var payload = JsonSerializer.Serialize(new { result = "success", data });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Builds a fake 200 response with an empty data array.</summary>
    private static HttpResponseMessage OkEmpty() => OkJson(Array.Empty<object>());

    /// <summary>Creates a provider backed by the given recording handler.</summary>
    private static (InfomaniakProvider Provider, RecordingHandler Handler) CreateProvider()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.infomaniak.com/1/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return (new InfomaniakProvider(http), handler);
    }

    // -------------------------------------------------------------------------
    // ListZonesAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListZonesAsync_ReturnsMappedZones()
    {
        var (provider, handler) = CreateProvider();

        handler.Enqueue(_ => OkJson(new[]
        {
            new { id = 1, customer_name = "example.com" },
            new { id = 2, customer_name = "other.net" }
        }));

        var zones = await provider.ListZonesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, zones.Count);
        Assert.Equal("1", zones[0].Id);
        Assert.Equal("example.com", zones[0].Name);
        Assert.Equal("2", zones[1].Id);
        Assert.Equal("other.net", zones[1].Name);
    }

    [Fact]
    public async Task ListZonesAsync_EmptyResponse_ReturnsEmptyList()
    {
        var (provider, handler) = CreateProvider();
        handler.Enqueue(_ => OkEmpty());

        var zones = await provider.ListZonesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(zones);
    }

    [Fact]
    public async Task ListZonesAsync_RequestHasBearerToken()
    {
        var (provider, handler) = CreateProvider();
        handler.Enqueue(_ => OkEmpty());

        await provider.ListZonesAsync(TestContext.Current.CancellationToken);

        var req = Assert.Single(handler.Requests);
        Assert.True(req.Headers.TryGetValue("Authorization", out var auth));
        Assert.Equal("Bearer test-token", auth[0]);
    }

    // -------------------------------------------------------------------------
    // CreateTxtRecordAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateTxtRecordAsync_SendsOneRequestPerValue()
    {
        var (provider, handler) = CreateProvider();

        // Two values → two POST requests
        handler.Enqueue(_ => OkJson(new { id = "r1" }));
        handler.Enqueue(_ => OkJson(new { id = "r2" }));

        var zone = new DnsZone(provider) { Id = "42", Name = "example.com" };

        await provider.CreateTxtRecordAsync(
            zone,
            "_acme-challenge",
            ["token-a", "token-b"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
    }

    [Fact]
    public async Task CreateTxtRecordAsync_PostBodyContainsExpectedFields()
    {
        var (provider, handler) = CreateProvider();
        handler.Enqueue(_ => OkJson(new { id = "r1" }));

        var zone = new DnsZone(provider) { Id = "42", Name = "example.com" };

        await provider.CreateTxtRecordAsync(
            zone,
            "_acme-challenge",
            ["my-token"],
            TestContext.Current.CancellationToken);

        var req = Assert.Single(handler.Requests);
        var body = JsonDocument.Parse(req.Content!);

        Assert.Equal("TXT", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("_acme-challenge", body.RootElement.GetProperty("source").GetString());
        Assert.Equal("my-token", body.RootElement.GetProperty("target").GetString());
        Assert.Equal(60, body.RootElement.GetProperty("ttl").GetInt32());
    }

    // -------------------------------------------------------------------------
    // DeleteTxtRecordAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteTxtRecordAsync_DeletesEachRecord()
    {
        var (provider, handler) = CreateProvider();

        // First: list records → two results
        handler.Enqueue(_ => OkJson(new[]
        {
            new { id = "r1", source = "_acme-challenge", target = "token-a" },
            new { id = "r2", source = "_acme-challenge", target = "token-b" }
        }));

        // Then: two DELETE requests
        handler.Enqueue(_ => OkJson(new { }));
        handler.Enqueue(_ => OkJson(new { }));

        var zone = new DnsZone(provider) { Id = "42", Name = "example.com" };

        await provider.DeleteTxtRecordAsync(
            zone,
            "_acme-challenge",
            TestContext.Current.CancellationToken);

        var deletes = handler.Requests.Where(r => r.Method == HttpMethod.Delete).ToList();
        Assert.Equal(2, deletes.Count);
        Assert.Contains(deletes, r => r.RequestUri!.ToString().EndsWith("/r1"));
        Assert.Contains(deletes, r => r.RequestUri!.ToString().EndsWith("/r2"));
    }

    [Fact]
    public async Task DeleteTxtRecordAsync_NoRecords_SendsNoDeleteRequest()
    {
        var (provider, handler) = CreateProvider();
        handler.Enqueue(_ => OkEmpty()); // list returns nothing

        var zone = new DnsZone(provider) { Id = "42", Name = "example.com" };

        await provider.DeleteTxtRecordAsync(
            zone,
            "_acme-challenge",
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task DeleteTxtRecordAsync_404OnDelete_IsIgnored()
    {
        var (provider, handler) = CreateProvider();

        handler.Enqueue(_ => OkJson(new[]
        {
            new { id = "r1", source = "_acme-challenge", target = "token-a" }
        }));

        // Simulate a 404 — should not throw
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var zone = new DnsZone(provider) { Id = "42", Name = "example.com" };

        await provider.DeleteTxtRecordAsync(
            zone,
            "_acme-challenge",
            TestContext.Current.CancellationToken);

        // If we reach here without exception, the test passes
    }

    // -------------------------------------------------------------------------
    // Provider metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Name_IsInfomaniak()
    {
        var (provider, _) = CreateProvider();
        Assert.Equal("Infomaniak", provider.Name);
    }

    [Fact]
    public void PropagationDelay_IsPositive()
    {
        var (provider, _) = CreateProvider();
        Assert.True(provider.PropagationDelay > TimeSpan.Zero);
    }
}

// ---------------------------------------------------------------------------
// Minimal recording handler (mirrors Acmebot.Acme.Tests.RecordingHandler)
// ---------------------------------------------------------------------------

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Enqueue a response factory that will be dequeued on the next HTTP call.</summary>
    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> factory)
        => _responses.Enqueue(factory);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await RecordedRequest.CreateAsync(request, cancellationToken));

        return _responses.TryDequeue(out var factory)
            ? factory(request)
            : throw new InvalidOperationException("No response was configured for this HTTP request.");
    }
}

/// <summary>Snapshot of an outgoing HTTP request for assertion purposes.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string? Content)
{
    public static async Task<RecordedRequest> CreateAsync(HttpRequestMessage req, CancellationToken ct) =>
        new(
            req.Method,
            req.RequestUri,
            req.Headers.ToDictionary(
                h => h.Key,
                h => (IReadOnlyList<string>)h.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase),
            req.Content is null ? null : await req.Content.ReadAsStringAsync(ct));
}
