using System.Buffers.Text;
using System.Net;
using System.Text.Json;

using Acmebot.Acme.Models;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeClientTests
{
    [Fact]
    public async Task GetDirectoryAsync_CachesDirectoryResponse()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newNonceUrl = new Uri("https://example.com/acme/new-nonce");
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(
            handler,
            newNonce: newNonceUrl,
            profiles: new Dictionary<string, string>
            {
                ["tlsserver"] = "TLS Server"
            });

        var first = await client.GetDirectoryAsync();
        var second = await client.GetDirectoryAsync();

        Assert.Same(first, second);
        Assert.Equal("TLS Server", first.Metadata?.Profiles["tlsserver"]);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task CreateAccountAsync_WithExternalAccountBinding_EmbedsBindingPayload()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newNonceUrl = new Uri("https://example.com/acme/new-nonce");
        var newAccountUrl = new Uri("https://example.com/acme/new-account");
        var accountUrl = new Uri("https://example.com/acme/account/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, newNonce: newNonceUrl, newAccount: newAccountUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.Created,
            new
            {
                status = "valid"
            },
            replayNonce: "bm9uY2Uy",
            location: accountUrl));

        var result = await client.CreateAccountAsync(
            signer,
            new AcmeNewAccountRequest
            {
                Contact = ["mailto:admin@example.com"],
                TermsOfServiceAgreed = true
            },
            new AcmeExternalAccountBindingOptions
            {
                KeyIdentifier = "kid-1",
                HmacKey = "secret-key"u8.ToArray(),
                Algorithm = "HS256"
            });

        var accountRequest = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = accountRequest.GetPayloadJson();
        var contact = payload.RootElement.GetProperty("contact");

        Assert.Equal("mailto:admin@example.com", Assert.Single(contact.EnumerateArray()).GetString());
        Assert.True(payload.RootElement.TryGetProperty("externalAccountBinding", out var externalAccountBinding));
        Assert.Equal(accountUrl, result.AccountUrl);
        Assert.Equal(AcmeAccountStatuses.Valid, result.Account.Status);
        Assert.False(string.IsNullOrWhiteSpace(externalAccountBinding.GetProperty("protected").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(externalAccountBinding.GetProperty("payload").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(externalAccountBinding.GetProperty("signature").GetString()));
    }

    [Fact]
    public async Task FindAccountAsync_SendsOnlyReturnExistingPayload()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var accountUrl = new Uri("https://example.com/acme/account/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.OK,
            new { status = "valid" },
            replayNonce: "bm9uY2Uy",
            location: accountUrl));

        var result = await client.FindAccountAsync(signer);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.True(payload.RootElement.GetProperty("onlyReturnExisting").GetBoolean());
        Assert.Equal(accountUrl, result.AccountUrl);
    }

    [Fact]
    public async Task GetAccountAsync_UsesPostAsGetAgainstAccountUrl()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            status = "valid",
            contact = new[] { "mailto:updated@example.com" }
        }, replayNonce: "bm9uY2Uy"));

        var result = await client.GetAccountAsync(account);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        Assert.Equal(account.AccountUrl, request.RequestUri);
        Assert.Equal(string.Empty, request.GetSignedMessage().Payload);
        Assert.Equal("mailto:updated@example.com", Assert.Single(result.Account.Contact));
    }

    [Fact]
    public async Task UpdateAccountAsync_SendsContactPayloadWithAccountKid()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            status = "valid",
            contact = new[] { "mailto:new@example.com" }
        }, replayNonce: "bm9uY2Uy"));

        var result = await client.UpdateAccountAsync(account, new AcmeUpdateAccountRequest { Contact = ["mailto:new@example.com"] });

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        using var protectedHeader = request.GetProtectedHeaderJson();
        Assert.Equal("mailto:new@example.com", Assert.Single(payload.RootElement.GetProperty("contact").EnumerateArray()).GetString());
        Assert.Equal(account.AccountUrl.OriginalString, protectedHeader.RootElement.GetProperty("kid").GetString());
        Assert.Equal("mailto:new@example.com", Assert.Single(result.Account.Contact));
    }

    [Fact]
    public async Task DeactivateAccountAsync_SendsDeactivatedStatus()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new { status = "deactivated" }, replayNonce: "bm9uY2Uy"));

        var result = await client.DeactivateAccountAsync(account);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal("deactivated", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal(AcmeAccountStatuses.Deactivated, result.Account.Status);
    }

    [Fact]
    public async Task ChangeAccountKeyAsync_SendsNestedJwsToKeyChangeEndpoint()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var keyChangeUrl = new Uri("https://example.com/acme/key-change");
        using var oldSigner = AcmeSigner.CreateP256();
        using var newSigner = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(oldSigner);

        AcmeTestSupport.EnqueueDirectory(handler, keyChange: keyChangeUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, string.Empty, contentType: "application/json", replayNonce: "bm9uY2Uy"));

        var result = await client.ChangeAccountKeyAsync(account, newSigner);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var outerProtectedHeader = request.GetProtectedHeaderJson();
        using var outerPayload = request.GetPayloadJson();
        var innerJws = JsonSerializer.Deserialize<AcmeSignedMessage>(outerPayload.RootElement.GetRawText());
        Assert.NotNull(innerJws);
        using var innerProtectedHeader = JsonDocument.Parse(AcmeTestSupport.DecodeBase64UrlUtf8(innerJws!.Protected));
        using var innerPayload = JsonDocument.Parse(AcmeTestSupport.DecodeBase64UrlUtf8(innerJws.Payload));

        Assert.Equal(keyChangeUrl, request.RequestUri);
        Assert.Equal(account.AccountUrl.OriginalString, outerProtectedHeader.RootElement.GetProperty("kid").GetString());
        Assert.Equal(newSigner.Algorithm, innerProtectedHeader.RootElement.GetProperty("alg").GetString());
        Assert.Equal(keyChangeUrl.OriginalString, innerProtectedHeader.RootElement.GetProperty("url").GetString());
        Assert.True(innerProtectedHeader.RootElement.TryGetProperty("jwk", out _));
        Assert.True(
            !innerProtectedHeader.RootElement.TryGetProperty("kid", out var innerKid)
            || innerKid.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.True(
            !innerProtectedHeader.RootElement.TryGetProperty("nonce", out var innerNonce)
            || innerNonce.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        Assert.Equal(account.AccountUrl.OriginalString, innerPayload.RootElement.GetProperty("account").GetString());
        Assert.Equal("EC", innerPayload.RootElement.GetProperty("oldKey").GetProperty("kty").GetString());
        Assert.Same(newSigner, result.Signer);
    }

    [Fact]
    public async Task CreateOrderAsync_ConvenienceOverload_SerializesIdentifiersAndOptionalFields()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var orderUrl = new Uri("https://example.com/acme/order/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);
        var notBefore = DateTimeOffset.Parse("2025-01-01T00:00:00+00:00");
        var notAfter = DateTimeOffset.Parse("2025-02-01T00:00:00+00:00");

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.Created,
            new
            {
                status = "pending",
                authorizations = new[] { "https://example.com/acme/authz/1" },
                finalize = "https://example.com/acme/finalize/1"
            },
            replayNonce: "bm9uY2Uy",
            location: orderUrl));

        var result = await client.CreateOrderAsync(
            account,
            [new AcmeIdentifier { Type = AcmeIdentifierTypes.Dns, Value = "example.com" }],
            profile: "tlsserver",
            replaces: "old-cert",
            notBefore: notBefore,
            notAfter: notAfter);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal("example.com", payload.RootElement.GetProperty("identifiers")[0].GetProperty("value").GetString());
        Assert.Equal("tlsserver", payload.RootElement.GetProperty("profile").GetString());
        Assert.Equal("old-cert", payload.RootElement.GetProperty("replaces").GetString());
        Assert.Equal(notBefore, payload.RootElement.GetProperty("notBefore").GetDateTimeOffset());
        Assert.Equal(notAfter, payload.RootElement.GetProperty("notAfter").GetDateTimeOffset());
        Assert.Equal(orderUrl, result.Location);
        Assert.Equal(AcmeOrderStatuses.Pending, result.Resource.Status);
    }

    [Fact]
    public async Task FinalizeOrderAsync_EncodesCsrInPayload()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var finalizeUrl = new Uri("https://example.com/acme/finalize/1");
        var csr = "csr-data"u8.ToArray();
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new { status = "processing" }, replayNonce: "bm9uY2Uy"));

        var result = await client.FinalizeOrderAsync(account, finalizeUrl, csr);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal(Base64Url.EncodeToString(csr), payload.RootElement.GetProperty("csr").GetString());
        Assert.Equal(AcmeOrderStatuses.Processing, result.Resource.Status);
    }

    [Fact]
    public async Task AnswerChallengeAsync_SendsEmptyObjectPayload()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var challengeUrl = new Uri("https://example.com/acme/challenge/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new { type = "http-01", url = challengeUrl, status = "pending" }, replayNonce: "bm9uY2Uy"));

        var result = await client.AnswerChallengeAsync(account, challengeUrl);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        Assert.Equal(JsonValueKind.Object, payload.RootElement.ValueKind);
        Assert.False(payload.RootElement.EnumerateObject().Any());
        Assert.Equal(AcmeChallengeStatuses.Pending, result.Resource.Status);
    }

    [Fact]
    public async Task RevokeCertificateAsync_WithAccountHandle_UsesAccountKid()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var revokeUrl = new Uri("https://example.com/acme/revoke-cert");
        var certificateDer = new byte[] { 1, 2, 3, 4 };
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);
        var account = AcmeTestSupport.CreateAccountHandle(signer);

        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            newNonce = new Uri("https://example.com/acme/new-nonce"),
            newAccount = new Uri("https://example.com/acme/new-account"),
            newOrder = new Uri("https://example.com/acme/new-order"),
            revokeCert = revokeUrl
        }));
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, string.Empty, contentType: null, replayNonce: "bm9uY2Ux"));
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, string.Empty, contentType: "application/json", replayNonce: "bm9uY2Uy"));

        await client.RevokeCertificateAsync(account, certificateDer, reason: 1);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var payload = request.GetPayloadJson();
        using var protectedHeader = request.GetProtectedHeaderJson();
        Assert.Equal(Base64Url.EncodeToString(certificateDer), payload.RootElement.GetProperty("certificate").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("reason").GetInt32());
        Assert.Equal(account.AccountUrl.OriginalString, protectedHeader.RootElement.GetProperty("kid").GetString());
    }

    [Fact]
    public async Task RevokeCertificateAsync_WithCertificateSigner_UsesJwkHeader()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var revokeUrl = new Uri("https://example.com/acme/revoke-cert");
        var certificateDer = new byte[] { 5, 6, 7, 8 };
        using var certificateSigner = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            newNonce = new Uri("https://example.com/acme/new-nonce"),
            newAccount = new Uri("https://example.com/acme/new-account"),
            newOrder = new Uri("https://example.com/acme/new-order"),
            revokeCert = revokeUrl
        }));
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, string.Empty, contentType: null, replayNonce: "bm9uY2Ux"));
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, string.Empty, contentType: "application/json", replayNonce: "bm9uY2Uy"));

        await client.RevokeCertificateAsync(certificateSigner, certificateDer, reason: 0);

        var request = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        using var protectedHeader = request.GetProtectedHeaderJson();
        Assert.True(protectedHeader.RootElement.TryGetProperty("jwk", out _));
        Assert.True(
            !protectedHeader.RootElement.TryGetProperty("kid", out var kid)
            || kid.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
    }

    [Fact]
    public async Task GetOrderAsync_RetriesBadNonceResponse()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var orderUrl = new Uri("https://example.com/acme/order/1");
        using var signer = AcmeSigner.CreateP256();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(
            httpClient,
            directoryUrl,
            new AcmeClientOptions
            {
                BadNonceRetryCount = 1
            });

        AcmeTestSupport.EnqueueDirectory(handler);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.BadRequest,
            new
            {
                type = AcmeProblemTypes.BadNonce.Value,
                detail = "bad nonce"
            },
            replayNonce: "bm9uY2Uy",
            contentType: "application/problem+json"));
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(
            HttpStatusCode.OK,
            new
            {
                status = "valid"
            },
            replayNonce: "bm9uY2Uz"));

        var result = await client.GetOrderAsync(AcmeTestSupport.CreateAccountHandle(signer), orderUrl);

        var postRequests = handler.Requests.Where(x => x.Method == HttpMethod.Post).ToArray();
        Assert.Equal(2, postRequests.Length);
        Assert.Single(handler.Requests, x => x.Method == HttpMethod.Head);
        Assert.Equal(AcmeOrderStatuses.Valid, result.Resource.Status);

        using var firstProtectedHeader = postRequests[0].GetProtectedHeaderJson();
        using var secondProtectedHeader = postRequests[1].GetProtectedHeaderJson();
        Assert.Equal("bm9uY2Ux", firstProtectedHeader.RootElement.GetProperty("nonce").GetString());
        Assert.False(string.IsNullOrWhiteSpace(secondProtectedHeader.RootElement.GetProperty("nonce").GetString()));
    }

    [Fact]
    public async Task GetRenewalInfoAsync_ThrowsForInvalidSuggestedWindow()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var renewalInfoUrl = new Uri("https://example.com/acme/renewal-info");
        const string certificateIdentifier = "AQID.f4CB";
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, renewalInfo: renewalInfoUrl);
        handler.Enqueue(_ => AcmeTestSupport.CreateJsonResponse(HttpStatusCode.OK, new
        {
            suggestedWindow = new
            {
                start = DateTimeOffset.UtcNow,
                end = DateTimeOffset.UtcNow.AddMinutes(-5)
            }
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetRenewalInfoAsync(certificateIdentifier));

        var renewalRequest = Assert.Single(handler.Requests, x => x.RequestUri == new Uri($"{renewalInfoUrl}/{certificateIdentifier}"));
        Assert.Equal(["application/json"], renewalRequest.AcceptMediaTypes);
        Assert.Equal("The ACME server returned an invalid renewalInfo suggestedWindow.", exception.Message);
    }

    [Fact]
    public async Task DownloadCertificateAsync_RequestsPemChainAndParsesCertificate()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newNonceUrl = new Uri("https://example.com/acme/new-nonce");
        var certificateUrl = new Uri("https://example.com/acme/certificate/1");
        var alternateCertificateUrl = new Uri("https://example.com/acme/certificate/alternate");
        var issuerCertificateUrl = new Uri("https://example.com/acme/issuer/1");
        using var signer = AcmeSigner.CreateP256();
        using var certificate = AcmeTestSupport.CreateCertificate();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, newNonce: newNonceUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ =>
        {
            var response = AcmeTestSupport.CreateResponse(
                HttpStatusCode.OK,
                certificate.ExportCertificatePem(),
                AcmeTestSupport.PemCertificateChainMediaType,
                replayNonce: "bm9uY2Uy");
            response.Headers.TryAddWithoutValidation("Link", $"<{alternateCertificateUrl}>;rel=\"alternate\"");
            response.Headers.TryAddWithoutValidation("Link", $"<{issuerCertificateUrl}>;rel=\"up\"");
            return response;
        });

        var result = await client.DownloadCertificateAsync(AcmeTestSupport.CreateAccountHandle(signer), certificateUrl);

        var certificateRequest = Assert.Single(handler.Requests, x => x.Method == HttpMethod.Post);
        Assert.Equal(certificateUrl, certificateRequest.RequestUri);
        Assert.Equal([AcmeTestSupport.PemCertificateChainMediaType], certificateRequest.AcceptMediaTypes);
        Assert.Equal("application/jose+json", certificateRequest.ContentType);
        Assert.Equal(certificate.Thumbprint, Assert.Single(result.Certificates).Thumbprint);
        Assert.Equal(certificate.ExportCertificatePem(), result.PemChain);
        Assert.Equal([alternateCertificateUrl], result.AlternateCertificateUrls);
        Assert.Equal([issuerCertificateUrl], result.IssuerCertificateUrls);
    }

    [Fact]
    public async Task DownloadCertificateAsync_ThrowsWhenServerReturnsNonPemContentType()
    {
        var directoryUrl = new Uri("https://example.com/acme/directory");
        var newNonceUrl = new Uri("https://example.com/acme/new-nonce");
        var certificateUrl = new Uri("https://example.com/acme/certificate/1");
        using var signer = AcmeSigner.CreateP256();
        using var certificate = AcmeTestSupport.CreateCertificate();
        using var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        using var client = new AcmeClient(httpClient, directoryUrl);

        AcmeTestSupport.EnqueueDirectory(handler, newNonce: newNonceUrl);
        AcmeTestSupport.EnqueueNonce(handler);
        handler.Enqueue(_ => AcmeTestSupport.CreateResponse(HttpStatusCode.OK, certificate.ExportCertificatePem(), "application/pkix-cert", replayNonce: "bm9uY2Uy"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.DownloadCertificateAsync(AcmeTestSupport.CreateAccountHandle(signer), certificateUrl));

        Assert.Equal(
            $"The ACME server returned 'application/pkix-cert' instead of '{AcmeTestSupport.PemCertificateChainMediaType}' for the certificate chain.",
            exception.Message);
    }

    [Fact]
    public void CreateCertificateIdentifier_ReturnsBase64UrlEncodedSegments()
    {
        ReadOnlySpan<byte> authorityKeyIdentifier = [0x01, 0x02, 0x03];
        ReadOnlySpan<byte> serialNumber = [0x7f, 0x80, 0x81];

        var identifier = AcmeClient.CreateCertificateIdentifier(authorityKeyIdentifier, serialNumber);

        Assert.Equal("AQID.f4CB", identifier);
    }
}
