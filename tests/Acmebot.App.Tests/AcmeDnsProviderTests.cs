using System.Net;
using System.Text.Json;

using Acmebot.App.Options;
using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AcmeDnsProviderTests
{
    [Theory]
    [InlineData("bücher.example", "xn--bcher-kva.example")]
    [InlineData("example.com", "EXAMPLE.COM")]
    public async Task CreateTxtRecordAsync_FindsNormalizedZone(string configuredName, string lookupName)
    {
        using var handler = new RecordingHandler();
        var provider = new AcmeDnsProvider(CreateOptions(configuredName), handler);
        var listedZone = Assert.Single(await provider.ListZonesAsync(TestContext.Current.CancellationToken));
        var zone = new DnsZone(provider) { Id = listedZone.Id, Name = lookupName };

        await provider.CreateTxtRecordAsync(zone, "_acme-challenge", ["token"], TestContext.Current.CancellationToken);

        Assert.Equal(lookupName.ToLowerInvariant(), listedZone.Name.ToLowerInvariant());
        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://acme-dns.example/api/update"), request.Uri);
        Assert.Equal("user", request.Username);
        Assert.Equal("password", request.Password);
        Assert.Equal("subdomain", request.Subdomain);
        Assert.Equal("token", request.Txt);
    }

    [Theory]
    [InlineData("bücher.example", "xn--bcher-kva.example")]
    [InlineData("example.com", "EXAMPLE.COM")]
    public void Constructor_RejectsEquivalentZoneNames(string firstName, string secondName)
    {
        var options = CreateOptions(firstName, secondName);

        var exception = Assert.Throws<InvalidOperationException>(() => new AcmeDnsProvider(options));

        Assert.Contains("zone names must be unique", exception.Message);
    }

    [Fact]
    public async Task CreateTxtRecordAsync_UpdatesTwoDistinctValuesOnceEach()
    {
        using var handler = new RecordingHandler();
        var provider = new AcmeDnsProvider(CreateOptions("example.com"), handler);
        var zone = Assert.Single(await provider.ListZonesAsync(TestContext.Current.CancellationToken));

        await provider.CreateTxtRecordAsync(zone, "_acme-challenge", ["apex", "wildcard", "apex"], TestContext.Current.CancellationToken);

        Assert.Equal(["apex", "wildcard"], handler.Requests.Select(x => x.Txt));
        Assert.All(handler.Responses, x => Assert.True(x.IsDisposed));
    }

    [Fact]
    public async Task CreateTxtRecordAsync_RejectsMoreThanTwoValuesBeforeUpdating()
    {
        using var handler = new RecordingHandler();
        var provider = new AcmeDnsProvider(CreateOptions("example.com"), handler);
        var zone = Assert.Single(await provider.ListZonesAsync(TestContext.Current.CancellationToken));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateTxtRecordAsync(zone, "_acme-challenge", ["one", "two", "three"], TestContext.Current.CancellationToken));

        Assert.Contains("at most two distinct TXT values", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateTxtRecordAsync_DisposesFailedResponseAndStopsUpdating()
    {
        using var handler = new RecordingHandler(HttpStatusCode.Forbidden);
        var provider = new AcmeDnsProvider(CreateOptions("example.com"), handler);
        var zone = Assert.Single(await provider.ListZonesAsync(TestContext.Current.CancellationToken));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            provider.CreateTxtRecordAsync(zone, "_acme-challenge", ["one", "two"], TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Single(handler.Requests);
        Assert.True(Assert.Single(handler.Responses).IsDisposed);
    }

    private static AcmeDnsOptions CreateOptions(params string[] names) => new()
    {
        Endpoint = "https://acme-dns.example/api",
        Zones = names.Select(name => new AcmeDnsZoneOptions
        {
            Name = name,
            Subdomain = "subdomain",
            Username = "user",
            Password = "password"
        }).ToArray()
    };

    private sealed record RecordedRequest(Uri? Uri, string Username, string Password, string? Subdomain, string? Txt);

    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public List<TrackingContent> Responses { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains(request.Headers.UserAgent, x => x.Product?.Name == "Acmebot");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Requests.Add(new RecordedRequest(request.RequestUri,
                Assert.Single(request.Headers.GetValues("X-Api-User")),
                Assert.Single(request.Headers.GetValues("X-Api-Key")),
                body.RootElement.GetProperty("subdomain").GetString(),
                body.RootElement.GetProperty("txt").GetString()));

            var content = new TrackingContent();
            Responses.Add(content);
            return new HttpResponseMessage(statusCode) { Content = content };
        }
    }

    private sealed class TrackingContent() : StringContent("{}")
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
