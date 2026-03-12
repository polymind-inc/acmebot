using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Acmebot.Acme.Models;

namespace Acmebot.Acme.Tests;

internal static class AcmeTestSupport
{
    public const string PemCertificateChainMediaType = "application/pem-certificate-chain";

    public static readonly Uri DefaultNewNonceUrl = new("https://example.com/acme/new-nonce");
    public static readonly Uri DefaultNewAccountUrl = new("https://example.com/acme/new-account");
    public static readonly Uri DefaultNewOrderUrl = new("https://example.com/acme/new-order");

    public static AcmeAccountHandle CreateAccountHandle(AcmeSigner signer)
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

    public static X509Certificate2 CreateCertificate(string subjectName = "example.com")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={subjectName}", key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    public static (X509Certificate2 Certificate, byte[] AuthorityKeyIdentifier, byte[] SerialNumber) CreateCertificateWithAuthorityKeyIdentifier()
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issuerRequest = new CertificateRequest("CN=Test CA", issuerKey, HashAlgorithmName.SHA256);
        issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        var issuerSubjectKeyIdentifier = new X509SubjectKeyIdentifierExtension(issuerRequest.PublicKey, false);
        issuerRequest.CertificateExtensions.Add(issuerSubjectKeyIdentifier);
        using var issuerCertificate = issuerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(365));

        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafRequest = new CertificateRequest("CN=leaf.example.com", leafKey, HashAlgorithmName.SHA256);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(leafRequest.PublicKey, false));
        var authorityKeyIdentifier = X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(issuerSubjectKeyIdentifier);
        leafRequest.CertificateExtensions.Add(authorityKeyIdentifier);
        byte[] serialNumber = [0x01, 0x23, 0x45, 0x67];
        var certificate = leafRequest.Create(issuerCertificate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), serialNumber);

        return (certificate, authorityKeyIdentifier.KeyIdentifier!.Value.ToArray(), serialNumber);
    }

    public static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload, string? replayNonce = null, Uri? location = null, string contentType = "application/json")
    {
        return CreateResponse(statusCode, JsonSerializer.Serialize(payload), contentType, replayNonce, location);
    }

    public static HttpResponseMessage CreateDirectoryResponse(
        Uri? newNonce = null,
        Uri? newAccount = null,
        Uri? newOrder = null,
        Uri? newAuthorization = null,
        Uri? revokeCertificate = null,
        Uri? keyChange = null,
        Uri? renewalInfo = null,
        IReadOnlyDictionary<string, string>? profiles = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["newNonce"] = newNonce ?? DefaultNewNonceUrl,
            ["newAccount"] = newAccount ?? DefaultNewAccountUrl,
            ["newOrder"] = newOrder ?? DefaultNewOrderUrl
        };

        if (newAuthorization is not null)
        {
            payload["newAuthz"] = newAuthorization;
        }

        if (revokeCertificate is not null)
        {
            payload["revokeCert"] = revokeCertificate;
        }

        if (keyChange is not null)
        {
            payload["keyChange"] = keyChange;
        }

        if (renewalInfo is not null)
        {
            payload["renewalInfo"] = renewalInfo;
        }

        if (profiles is not null)
        {
            payload["meta"] = new Dictionary<string, object?>
            {
                ["profiles"] = profiles
            };
        }

        return CreateJsonResponse(HttpStatusCode.OK, payload);
    }

    public static void EnqueueDirectory(
        RecordingHandler handler,
        Uri? newNonce = null,
        Uri? newAccount = null,
        Uri? newOrder = null,
        Uri? newAuthorization = null,
        Uri? revokeCertificate = null,
        Uri? keyChange = null,
        Uri? renewalInfo = null,
        IReadOnlyDictionary<string, string>? profiles = null)
    {
        handler.Enqueue(_ => CreateDirectoryResponse(
            newNonce,
            newAccount,
            newOrder,
            newAuthorization,
            revokeCertificate,
            keyChange,
            renewalInfo,
            profiles));
    }

    public static void EnqueueNonce(RecordingHandler handler, string replayNonce = "bm9uY2Ux")
    {
        handler.Enqueue(_ => CreateResponse(HttpStatusCode.OK, string.Empty, contentType: null, replayNonce: replayNonce));
    }

    public static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, string? contentType, string? replayNonce = null, Uri? location = null)
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

    public static string DecodeBase64UrlUtf8(string value)
    {
        return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responses.Enqueue(responseFactory);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await RecordedRequest.CreateAsync(request, cancellationToken));

        return _responses.TryDequeue(out var responseFactory)
            ? responseFactory(request)
            : throw new InvalidOperationException("No response was configured for the HTTP request.");
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    IReadOnlyList<string> AcceptMediaTypes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string? ContentType,
    string? Content)
{
    public AcmeSignedMessage GetSignedMessage()
    {
        return JsonSerializer.Deserialize<AcmeSignedMessage>(Content!)
            ?? throw new InvalidOperationException("The request body did not contain a signed ACME message.");
    }

    public JsonDocument GetPayloadJson()
    {
        return JsonDocument.Parse(AcmeTestSupport.DecodeBase64UrlUtf8(GetSignedMessage().Payload));
    }

    public JsonDocument GetProtectedHeaderJson()
    {
        return JsonDocument.Parse(AcmeTestSupport.DecodeBase64UrlUtf8(GetSignedMessage().Protected));
    }

    public static async Task<RecordedRequest> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Accept.Select(x => x.MediaType ?? string.Empty).ToArray(),
            request.Headers.ToDictionary(
                static header => header.Key,
                static header => (IReadOnlyList<string>)header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase),
            request.Content?.Headers.ContentType?.MediaType,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
    }
}
