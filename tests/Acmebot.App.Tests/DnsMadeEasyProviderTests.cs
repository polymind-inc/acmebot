using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Acmebot.App.Options;
using Acmebot.App.Providers;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class DnsMadeEasyProviderTests
{
    private const string ApiKey = "test-api-key";

    [Theory]
    [InlineData("test-secret")]
    [InlineData("sëcret-秘密")]
    public async Task ListZonesAsync_SignsRequestDateWithConfiguredSecret(string secret)
    {
        using var handler = new SignatureValidationHandler(secret);
        var provider = new DnsMadeEasyProvider(new DnsMadeEasyOptions { ApiKey = ApiKey, SecretKey = secret }, handler);

        var zones = await provider.ListZonesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(zones);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ListZonesAsync_ConcurrentRequests_AllHaveValidSignatures()
    {
        const int workerCount = 16;
        const int requestsPerWorker = 256;
        const string secret = "parallel-test-secret";
        using var handler = new SignatureValidationHandler(secret);
        var provider = new DnsMadeEasyProvider(new DnsMadeEasyOptions { ApiKey = ApiKey, SecretKey = secret }, handler);
        using var ready = new CountdownEvent(workerCount);
        using var start = new ManualResetEventSlim();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Dedicated threads exercise signing simultaneously, including on runners with a small thread pool.
        var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait(cancellationToken);

            for (var index = 0; index < requestsPerWorker; index++)
            {
                provider.ListZonesAsync(cancellationToken).GetAwaiter().GetResult();
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        }
        finally
        {
            start.Set();
        }

        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        Assert.Equal(workerCount * requestsPerWorker, handler.RequestCount);
    }

    private sealed class SignatureValidationHandler(string secret) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Assert.Equal(ApiKey, Assert.Single(request.Headers.GetValues("x-dnsme-apiKey")));
            var date = Assert.Single(request.Headers.GetValues("x-dnsme-requestDate"));
            Assert.True(DateTimeOffset.TryParseExact(date, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _));

            // Independently calculate the protocol's HMAC with a fresh instance for each request.
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(date)));
            Assert.Equal(expected, Assert.Single(request.Headers.GetValues("x-dnsme-hmac")));
            Interlocked.Increment(ref _requestCount);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { data = Array.Empty<object>(), totalPages = 1 })
            });
        }
    }
}
