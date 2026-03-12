using System.Buffers.Text;
using System.Net;

using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeClientResourceTests
{
    [Fact]
    public async Task GetOrdersAsync_UsesOrdersUrlFromAccountResource()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var ordersUrl = new Uri("https://example.com/acme/account/1/orders");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = new AcmeAccountHandle
        {
            AccountUrl = new Uri("https://example.com/acme/account/1"),
            Signer = signer,
            Account = new AcmeAccountResource
            {
                Status = AcmeAccountStatuses.Valid,
                Orders = ordersUrl
            }
        };

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            orders = new[] { "https://example.com/acme/order/1" }
        }, replayNonce: "bm9uY2Uy"));

        var result = await client.GetOrdersAsync(account);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        Assert.Equal(ordersUrl, request.RequestUri);
        Assert.Equal(string.Empty, request.GetSignedMessage().Payload);
        Assert.Equal(new Uri("https://example.com/acme/order/1"), Assert.Single(result.Resource.Orders));
    }

    [Fact]
    public async Task CreateOrderAsync_RequestOverload_SerializesPayload()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var orderUrl = new Uri("https://example.com/acme/order/2");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);
        var requestModel = new AcmeNewOrderRequest
        {
            Identifiers = [new AcmeIdentifier { Type = AcmeIdentifierTypes.Dns, Value = "example.net" }],
            Profile = "mailserver"
        };

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.Created,
            new { status = "pending" },
            replayNonce: "bm9uY2Uy",
            location: orderUrl));

        var result = await client.CreateOrderAsync(account, requestModel);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal("example.net", payload.RootElement.GetProperty("identifiers")[0].GetProperty("value").GetString());
        Assert.Equal("mailserver", payload.RootElement.GetProperty("profile").GetString());
        Assert.Equal(orderUrl, result.Location);
    }

    [Fact]
    public async Task CreateAuthorizationAsync_SendsIdentifierPayload()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var authorizationUrl = new Uri("https://example.com/acme/authz/1");
        var newAuthorizationUrl = new Uri("https://example.com/acme/new-authz");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler, newAuthorization: newAuthorizationUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.Created,
            new
            {
                identifier = new { type = "dns", value = "example.org" },
                status = "pending",
                challenges = Array.Empty<object>()
            },
            replayNonce: "bm9uY2Uy",
            location: authorizationUrl));

        var result = await client.CreateAuthorizationAsync(account, new AcmeNewAuthorizationRequest
        {
            Identifier = new AcmeIdentifier
            {
                Type = AcmeIdentifierTypes.Dns,
                Value = "example.org"
            }
        });

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal(newAuthorizationUrl, request.RequestUri);
        Assert.Equal("dns", payload.RootElement.GetProperty("identifier").GetProperty("type").GetString());
        Assert.Equal("example.org", payload.RootElement.GetProperty("identifier").GetProperty("value").GetString());
        Assert.Equal(authorizationUrl, result.Location);
    }

    [Fact]
    public async Task GetAuthorizationAsync_UsesPostAsGet()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var authorizationUrl = new Uri("https://example.com/acme/authz/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            identifier = new { type = "dns", value = "example.org" },
            status = "valid",
            challenges = Array.Empty<object>()
        }, replayNonce: "bm9uY2Uy"));

        var result = await client.GetAuthorizationAsync(account, authorizationUrl);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        Assert.Equal(authorizationUrl, request.RequestUri);
        Assert.Equal(string.Empty, request.GetSignedMessage().Payload);
        Assert.Equal(AcmeAuthorizationStatuses.Valid, result.Resource.Status);
    }

    [Fact]
    public async Task DeactivateAuthorizationAsync_SendsDeactivatedStatus()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var authorizationUrl = new Uri("https://example.com/acme/authz/2");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            identifier = new { type = "dns", value = "example.org" },
            status = "deactivated",
            challenges = Array.Empty<object>()
        }, replayNonce: "bm9uY2Uy"));

        var result = await client.DeactivateAuthorizationAsync(account, authorizationUrl);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal("deactivated", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(AcmeAuthorizationStatuses.Deactivated, result.Resource.Status);
    }

    [Fact]
    public async Task GetChallengeAsync_UsesPostAsGet()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var challengeUrl = new Uri("https://example.com/acme/challenge/2");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            type = "dns-01",
            url = challengeUrl,
            status = "valid"
        }, replayNonce: "bm9uY2Uy"));

        var result = await client.GetChallengeAsync(account, challengeUrl);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        Assert.Equal(challengeUrl, request.RequestUri);
        Assert.Equal(string.Empty, request.GetSignedMessage().Payload);
        Assert.Equal(AcmeChallengeStatuses.Valid, result.Resource.Status);
    }

    [Fact]
    public async Task ProfileOperations_UseCachedDirectoryMetadata()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(
            handler,
            profiles: new Dictionary<string, string>
            {
                ["tlsserver"] = "TLS Server"
            });

        var profiles = await client.GetAdvertisedProfilesAsync();
        var advertised = await client.IsProfileAdvertisedAsync("tlsserver");
        await client.EnsureProfileIsAdvertisedAsync("tlsserver");

        Assert.Equal("TLS Server", profiles["tlsserver"]);
        Assert.True(advertised);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task EnsureProfileIsAdvertisedAsync_ThrowsForMissingProfile()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, profiles: new Dictionary<string, string>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.EnsureProfileIsAdvertisedAsync("missing"));

        Assert.Equal("The ACME server does not advertise the 'missing' profile.", exception.Message);
    }

    [Fact]
    public async Task GetRenewalInfoAsync_CertificateOverload_UsesDerivedIdentifier()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var renewalInfoUrl = new Uri("https://example.com/acme/renewal-info");
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        using var certificate = AcmeTestSupport.CreateCertificateWithAuthorityKeyIdentifier().Certificate;
        var expectedIdentifier = AcmeClient.CreateCertificateIdentifier(certificate);

        AcmeTestSupport.EnqueueDirectory(handler, renewalInfo: renewalInfoUrl);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            suggestedWindow = new
            {
                start = DateTimeOffset.UtcNow,
                end = DateTimeOffset.UtcNow.AddHours(1)
            }
        }));

        _ = await client.GetRenewalInfoAsync(certificate);

        Assert.Single(handler.Requests, x => x.RequestUri == new Uri($"{renewalInfoUrl}/{expectedIdentifier}"));
    }

    [Fact]
    public void CreateCertificateIdentifier_CertificateOverload_ReturnsBase64UrlEncodedSegments()
    {
        var created = AcmeTestSupport.CreateCertificateWithAuthorityKeyIdentifier();
        using var certificate = created.Certificate;

        var identifier = AcmeClient.CreateCertificateIdentifier(certificate);

        Assert.Equal($"{Base64Url.EncodeToString(created.AuthorityKeyIdentifier)}.{Base64Url.EncodeToString(created.SerialNumber)}", identifier);
    }
}
