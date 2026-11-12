using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Acmebot.App.Options;
using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class EasyDnsProviderTests
{
    private static readonly EasyDnsOptions s_options = new() { ApiToken = "test-token", ApiKey = "test-key" };

    [Fact]
    public async Task ListZonesAsync_SendsBasicCredentialsAndReturnsDomains()
    {
        using var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/user" => Json(new { data = new { user = "test-user" } }),
            "/domains/list/test-user" => Json(new
            {
                data = new Dictionary<string, object>
                {
                    ["0"] = new { name = "example.com" },
                    ["1"] = new { name = "example.net" },
                    ["user"] = "test-user"
                }
            }),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var provider = new EasyDnsProvider(s_options, handler);

        var zones = await provider.ListZonesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["example.com", "example.net"], zones.Select(x => x.Name));
        Assert.Equal(["example.com", "example.net"], zones.Select(x => x.Id));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("Basic", request.Authorization?.Scheme);
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("test-token:test-key")), request.Authorization?.Parameter);
        });
    }

    [Fact]
    public async Task ListZonesAsync_WithoutUser_Throws()
    {
        using var handler = new RecordingHandler(_ => Json(new { data = new { } }));
        var provider = new EasyDnsProvider(s_options, handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ListZonesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateTxtRecordAsync_PutsEachValue()
    {
        using var handler = new RecordingHandler(_ => Json(new { data = new { } }));
        var provider = new EasyDnsProvider(s_options, handler);
        var zone = new DnsZone { Id = "example.com", Name = "example.com" };

        await provider.CreateTxtRecordAsync(zone, "_acme-challenge", ["value-1", "value-2"], TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/zones/records/add/example.com/TXT", request.Path);
        });

        var bodies = handler.Requests.Select(x => JsonDocument.Parse(x.Body!).RootElement).ToArray();

        Assert.Equal(["value-1", "value-2"], bodies.Select(x => x.GetProperty("rdata").GetString()));
        Assert.All(bodies, body =>
        {
            Assert.Equal("example.com", body.GetProperty("domain").GetString());
            Assert.Equal("_acme-challenge", body.GetProperty("host").GetString());
            Assert.Equal("TXT", body.GetProperty("type").GetString());
            Assert.Equal(60, body.GetProperty("ttl").GetInt32());
        });
    }

    [Fact]
    public async Task DeleteTxtRecordAsync_DeletesOnlyMatchingTxtRecordsAndIgnoresNotFound()
    {
        using var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? Json(new
            {
                data = new object[]
                {
                    new { id = "101", domain = "example.com", host = "_acme-challenge", type = "TXT", rdata = "a", ttl = "60", prio = "0" },
                    new { id = "102", domain = "example.com", host = "_acme-challenge", type = "TXT", rdata = "b", ttl = "60", prio = "0" },
                    new { id = "103", domain = "example.com", host = "_acme-challenge", type = "CNAME", rdata = "c", ttl = "60", prio = "0" },
                    new { id = "104", domain = "example.com", host = "www", type = "TXT", rdata = "d", ttl = "60", prio = "0" }
                }
            })
            : request.RequestUri!.AbsolutePath.EndsWith("/101", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK));
        var provider = new EasyDnsProvider(s_options, handler);
        var zone = new DnsZone { Id = "example.com", Name = "example.com" };

        await provider.DeleteTxtRecordAsync(zone, "_acme-challenge", TestContext.Current.CancellationToken);

        Assert.Equal("/zones/records/all/example.com", handler.Requests[0].Path);
        Assert.Equal(
            ["/zones/records/example.com/101", "/zones/records/example.com/102"],
            handler.Requests.Where(x => x.Method == HttpMethod.Delete).Select(x => x.Path));
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed record RecordedRequest(HttpMethod Method, string Path, AuthenticationHeaderValue? Authorization, string? Body);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization, body));

            return responder(request);
        }
    }
}
