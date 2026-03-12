using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeClientErrorTests
{
    [Fact]
    public async Task GetOrdersAsync_ThrowsWhenOrdersUrlIsMissing()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetOrdersAsync(account));

        Assert.Equal("The account resource does not include an orders URL.", exception.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ChangeAccountKeyAsync_ThrowsWhenDirectoryDoesNotAdvertiseKeyChange()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var oldSigner = AcmeSigner.CreateP256();
        using var newSigner = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(oldSigner);

        AcmeTestSupport.EnqueueDirectory(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChangeAccountKeyAsync(account, newSigner));

        Assert.Equal("The ACME server does not advertise the keyChange resource.", exception.Message);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task CreateAuthorizationAsync_ThrowsWhenDirectoryDoesNotAdvertiseNewAuthorization()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CreateAuthorizationAsync(
            account,
            new AcmeNewAuthorizationRequest
            {
                Identifier = new AcmeIdentifier
                {
                    Type = AcmeIdentifierTypes.Dns,
                    Value = "example.org"
                }
            }));

        Assert.Equal("The ACME server does not advertise the newAuthz resource.", exception.Message);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task GetRenewalInfoAsync_ThrowsWhenDirectoryDoesNotAdvertiseRenewalInfo()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetRenewalInfoAsync("AQID.f4CB"));

        Assert.Equal("The ACME server does not advertise the renewalInfo resource.", exception.Message);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task RevokeCertificateAsync_WithAccountHandle_ThrowsWhenDirectoryDoesNotAdvertiseRevokeCert()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RevokeCertificateAsync(account, new byte[] { 1, 2, 3, 4 }));

        Assert.Equal("The ACME server does not advertise the revokeCert resource.", exception.Message);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task RevokeCertificateAsync_WithCertificateSigner_ThrowsWhenDirectoryDoesNotAdvertiseRevokeCert()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var certificateSigner = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.RevokeCertificateAsync(certificateSigner, new byte[] { 5, 6, 7, 8 }));

        Assert.Equal("The ACME server does not advertise the revokeCert resource.", exception.Message);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }
}
