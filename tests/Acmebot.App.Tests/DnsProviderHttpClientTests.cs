using System.Net;
using System.Text;

using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsProviderHttpClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_WithHttpClientTransport_DisablesRedirects(bool useSigningHandler)
    {
        using var transport = new HttpClientHandler();
        using var handler = useSigningHandler ? (HttpMessageHandler)new PassThroughHandler(transport) : transport;
        using var client = DnsProviderHttpClient.Create("https://dns.example/", handler);

        Assert.False(transport.AllowAutoRedirect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WithRedirectResponse_DoesNotForwardApiKey(bool useSigningHandler)
    {
        var connectedHosts = new List<string>();
        using var transport = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = (context, _) =>
            {
                connectedHosts.Add(context.DnsEndPoint.Host);
                Assert.Equal("dns.example", context.DnsEndPoint.Host);
                // Use an in-memory HTTP connection so this test cannot send credentials over the network.
                return ValueTask.FromResult<Stream>(new RedirectResponseStream());
            }
        };
        using var handler = useSigningHandler ? (HttpMessageHandler)new PassThroughHandler(transport) : transport;
        using var client = DnsProviderHttpClient.Create("http://dns.example/", handler);
        client.DefaultRequestHeaders.Add("X-API-Key", "test-api-key");

        using var response = await client.GetAsync("zones", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(new Uri("http://other.example/capture"), response.Headers.Location);
        Assert.Equal(["dns.example"], connectedHosts);
    }

    [Fact]
    public void Create_SetsBaseAddressAndJsonAccept()
    {
        using var httpClient = DnsProviderHttpClient.Create("https://api.example.com/v1/");

        Assert.Equal(new Uri("https://api.example.com/v1/"), httpClient.BaseAddress);
        Assert.Contains(httpClient.DefaultRequestHeaders.Accept, x => x.MediaType == "application/json");
    }

    [Fact]
    public void Create_SendsAcmebotUserAgent()
    {
        using var httpClient = DnsProviderHttpClient.Create("https://api.example.com/v1/");

        var userAgent = Assert.Single(httpClient.DefaultRequestHeaders.UserAgent);

        Assert.Equal("Acmebot", userAgent.Product?.Name);
        Assert.False(string.IsNullOrEmpty(userAgent.Product?.Version));
    }

    [Fact]
    public void Create_WithHandler_SendsAcmebotUserAgent()
    {
        using var httpClient = DnsProviderHttpClient.Create("https://api.example.com/v1/", new HttpClientHandler());

        Assert.Contains(httpClient.DefaultRequestHeaders.UserAgent, x => x.Product?.Name == "Acmebot");
    }

    [Fact]
    public void AddUserAgent_AppliesToClientCreatedElsewhere()
    {
        using var httpClient = new HttpClient();

        DnsProviderHttpClient.AddUserAgent(httpClient);

        Assert.Contains(httpClient.DefaultRequestHeaders.UserAgent, x => x.Product?.Name == "Acmebot");
    }

    private sealed class PassThroughHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler);

    private sealed class RedirectResponseStream : Stream
    {
        private readonly MemoryStream _response = new(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: http://other.example/capture\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _response.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _response.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) { }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
