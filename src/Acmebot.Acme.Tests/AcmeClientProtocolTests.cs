using System.Net;

using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeClientProtocolTests
{
    [Fact]
    public async Task GetDirectoryAsync_AppliesConfiguredHeaders()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(
            httpClient,
            directoryUrl,
            new AcmeClientOptions
            {
                UserAgent = "Acmebot.Acme.Tests/1.0",
                AcceptLanguage = "ja-JP"
            });

        AcmeTestSupport.EnqueueDirectory(handler);

        _ = await client.GetDirectoryAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Acmebot.Acme.Tests/1.0", Assert.Single(request.Headers["User-Agent"]));
        Assert.Equal("ja-JP", Assert.Single(request.Headers["Accept-Language"]));
    }

    [Fact]
    public async Task GetOrderAsync_ThrowsProtocolExceptionWhenReplayNonceHeaderIsMissing()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newNonceUrl = new Uri("https://example.com/acme/new-nonce");
        var orderUrl = new Uri("https://example.com/acme/order/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, newNonce: newNonceUrl);
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, string.Empty, contentType: null));

        var exception = await Assert.ThrowsAsync<AcmeProtocolException>(() => client.GetOrderAsync(AcmeTestSupport.CreateAccountHandle(signer), orderUrl));

        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
        Assert.Equal(newNonceUrl, exception.RequestUri);
        Assert.Equal("The ACME server did not provide a Replay-Nonce header.", exception.Message);
    }

    [Fact]
    public async Task CreateAccountAsync_ThrowsProtocolExceptionWhenLocationHeaderIsMissing()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newAccountUrl = new Uri("https://example.com/acme/new-account");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, newAccount: newAccountUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.Created, new { status = "valid" }, replayNonce: "bm9uY2Uy"));

        var exception = await Assert.ThrowsAsync<AcmeProtocolException>(() => client.CreateAccountAsync(
            signer,
            new AcmeNewAccountRequest
            {
                Contact = ["mailto:admin@example.com"],
                TermsOfServiceAgreed = true
            }));

        Assert.Equal(HttpStatusCode.Created, exception.StatusCode);
        Assert.Equal(newAccountUrl, exception.RequestUri);
        Assert.Equal("The ACME server did not return an account URL.", exception.Message);
    }

    [Fact]
    public async Task DownloadCertificateAsync_ThrowsWhenPemContainsUnexpectedLabel()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var certificateUrl = new Uri("https://example.com/acme/certificate/1");
        const string nonCertificatePem = "-----BEGIN PRIVATE KEY-----\nAQID\n-----END PRIVATE KEY-----";
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, nonCertificatePem, AcmeTestSupport.PemCertificateChainMediaType, replayNonce: "bm9uY2Uy"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadCertificateAsync(AcmeTestSupport.CreateAccountHandle(signer), certificateUrl));

        Assert.Equal("The PEM response contains data other than certificates.", exception.Message);
    }
}
