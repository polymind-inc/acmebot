using System.Buffers.Text;
using System.Formats.Asn1;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Acmebot.Acme.Internal;
using Acmebot.Acme.Models;

namespace Acmebot.Acme;

public sealed class AcmeClient : IDisposable
{
    private const string JsonMediaType = "application/json";
    private const string JoseMediaType = "application/jose+json";
    private const string PemCertificateChainMediaType = "application/pem-certificate-chain";

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        TypeInfoResolver = AcmeJsonSerializerContext.Default
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _directoryUrl;
    private readonly AcmeClientOptions _options;
    private readonly AcmeNonceStore _nonceStore = new();
    private readonly SemaphoreSlim _directoryLock = new(1, 1);
    private readonly bool _ownsHttpClient;

    private AcmeDirectoryResource? _directory;
    private bool _disposed;

    public AcmeClient(Uri directoryUrl, AcmeClientOptions? options = null)
        : this(new HttpClient(), directoryUrl, options, ownsHttpClient: true)
    {
    }

    public AcmeClient(HttpClient httpClient, Uri directoryUrl, AcmeClientOptions? options = null)
        : this(httpClient, directoryUrl, options, ownsHttpClient: false)
    {
    }

    private AcmeClient(HttpClient httpClient, Uri directoryUrl, AcmeClientOptions? options, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(directoryUrl);

        if (!directoryUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("The ACME directory URL must be absolute.", nameof(directoryUrl));
        }

        _httpClient = httpClient;
        _directoryUrl = directoryUrl;
        _options = options ?? new AcmeClientOptions();
        _ownsHttpClient = ownsHttpClient;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _directoryLock.Dispose();

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public async Task<AcmeDirectoryResource> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AcmeAccountHandle> CreateAccountAsync(
        AcmeSigner signer,
        AcmeNewAccountRequest request,
        AcmeExternalAccountBindingOptions? externalAccountBinding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(request);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        var payload = SerializeUtf8(request);
        var signedPayload = payload;

        if (externalAccountBinding is not null)
        {
            var envelope = DeserializeUtf8<JsonObjectAccountRequest>(payload);
            envelope = envelope with
            {
                ExternalAccountBinding = CreateExternalAccountBinding(directory.NewAccount, externalAccountBinding, signer)
            };

            signedPayload = SerializeUtf8(envelope);
        }

        var response = await SendSignedRequestAsync(
            directory.NewAccount,
            signer,
            keyId: null,
            signedPayload,
            cancellationToken).ConfigureAwait(false);

        var resource = DeserializeUtf8<AcmeAccountResource>(response.Body);
        var location = response.Location ?? throw new AcmeProtocolException(
            response.StatusCode,
            "The ACME server did not return an account URL.",
            directory.NewAccount,
            replayNonce: response.ReplayNonce,
            retryAfter: response.RetryAfter,
            links: response.Links);

        return CreateAccountHandle(location, signer, resource);
    }

    public Task<AcmeAccountHandle> FindAccountAsync(AcmeSigner signer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signer);

        return CreateAccountAsync(
            signer,
            new AcmeNewAccountRequest
            {
                OnlyReturnExisting = true
            },
            externalAccountBinding: null,
            cancellationToken);
    }

    public async Task<AcmeAccountHandle> GetAccountAsync(AcmeAccountHandle account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var response = await SendPostAsGetAsync(account.Signer, account.AccountUrl, account.AccountUrl, cancellationToken).ConfigureAwait(false);
        var resource = DeserializeUtf8<AcmeAccountResource>(response.Body);

        return CreateAccountHandle(account, resource);
    }

    public async Task<AcmeAccountHandle> UpdateAccountAsync(
        AcmeAccountHandle account,
        AcmeUpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(request);

        var response = await SendSignedRequestAsync(
            account.AccountUrl,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(request),
            cancellationToken).ConfigureAwait(false);

        var resource = DeserializeUtf8<AcmeAccountResource>(response.Body);

        return CreateAccountHandle(account, resource);
    }

    public async Task<AcmeAccountHandle> DeactivateAccountAsync(AcmeAccountHandle account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var response = await SendSignedRequestAsync(
            account.AccountUrl,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(new AcmeAccountStatusUpdateRequest { Status = AcmeAccountStatuses.Deactivated }),
            cancellationToken).ConfigureAwait(false);

        var resource = DeserializeUtf8<AcmeAccountResource>(response.Body);

        return CreateAccountHandle(account, resource);
    }

    public async Task<AcmeAccountHandle> ChangeAccountKeyAsync(
        AcmeAccountHandle account,
        AcmeSigner newSigner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(newSigner);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (directory.KeyChange is null)
        {
            throw new InvalidOperationException("The ACME server does not advertise the keyChange resource.");
        }

        var innerPayload = SerializeUtf8(
            new AcmeKeyChangeRequest
            {
                Account = account.AccountUrl,
                OldKey = account.Signer.ExportJsonWebKey()
            });

        var innerJws = CreateSignedMessage(directory.KeyChange, newSigner, keyId: null, innerPayload, nonce: null);
        var outerPayload = SerializeUtf8(innerJws);

        var response = await SendSignedRequestAsync(
            directory.KeyChange,
            account.Signer,
            account.AccountUrl,
            outerPayload,
            cancellationToken).ConfigureAwait(false);

        var resource = response.Body.Length == 0
            ? account.Account
            : DeserializeUtf8<AcmeAccountResource>(response.Body);

        return CreateAccountHandle(account, resource, newSigner);
    }

    public async Task<AcmeResult<AcmeOrderListResource>> GetOrdersAsync(
        AcmeAccountHandle account,
        Uri? ordersUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var targetUrl = ordersUrl ?? account.Account.Orders ?? throw new InvalidOperationException("The account resource does not include an orders URL.");
        var response = await SendPostAsGetAsync(account.Signer, account.AccountUrl, targetUrl, cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeOrderListResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeOrderResource>> CreateOrderAsync(
        AcmeAccountHandle account,
        AcmeNewOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(request);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        var response = await SendSignedRequestAsync(
            directory.NewOrder,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(request),
            cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeOrderResource>(response.Body));
    }

    public Task<AcmeResult<AcmeOrderResource>> CreateOrderAsync(
        AcmeAccountHandle account,
        IReadOnlyList<AcmeIdentifier> identifiers,
        string? profile = null,
        string? replaces = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        return CreateOrderAsync(
            account,
            new AcmeNewOrderRequest
            {
                Identifiers = identifiers,
                NotBefore = notBefore,
                NotAfter = notAfter,
                Replaces = replaces,
                Profile = profile
            },
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAdvertisedProfilesAsync(CancellationToken cancellationToken = default)
    {
        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return AcmeProfileValidation.GetAdvertisedProfiles(directory);
    }

    public async Task<bool> IsProfileAdvertisedAsync(string profile, CancellationToken cancellationToken = default)
    {
        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        return AcmeProfileValidation.IsProfileAdvertised(directory, profile);
    }

    public async Task EnsureProfileIsAdvertisedAsync(string profile, CancellationToken cancellationToken = default)
    {
        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        AcmeProfileValidation.EnsureProfileIsAdvertised(directory, profile);
    }

    public async Task<AcmeResult<AcmeOrderResource>> GetOrderAsync(
        AcmeAccountHandle account,
        Uri orderUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(orderUrl);

        var response = await SendPostAsGetAsync(account.Signer, account.AccountUrl, orderUrl, cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeOrderResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeRenewalInfoResource>> GetRenewalInfoAsync(
        string certificateIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateIdentifier);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (directory.RenewalInfo is null)
        {
            throw new InvalidOperationException("The ACME server does not advertise the renewalInfo resource.");
        }

        var requestUrl = CreateResourceUrl(directory.RenewalInfo, certificateIdentifier);
        var response = await SendBootstrapRequestAsync(HttpMethod.Get, requestUrl, cancellationToken, JsonMediaType).ConfigureAwait(false);
        var resource = DeserializeUtf8<AcmeRenewalInfoResource>(response.Body);

        if (resource.SuggestedWindow.End <= resource.SuggestedWindow.Start)
        {
            throw new InvalidOperationException("The ACME server returned an invalid renewalInfo suggestedWindow.");
        }

        return CreateResult(response, resource);
    }

    public Task<AcmeResult<AcmeRenewalInfoResource>> GetRenewalInfoAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return GetRenewalInfoAsync(CreateCertificateIdentifier(certificate), cancellationToken);
    }

    public static string CreateCertificateIdentifier(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var authorityKeyIdentifier = GetAuthorityKeyIdentifier(certificate);
        var serialNumber = GetDerEncodedSerialNumber(certificate.RawData);

        return CreateCertificateIdentifier(authorityKeyIdentifier, serialNumber);
    }

    public static string CreateCertificateIdentifier(ReadOnlySpan<byte> authorityKeyIdentifier, ReadOnlySpan<byte> serialNumber)
    {
        if (authorityKeyIdentifier.IsEmpty)
        {
            throw new ArgumentException("The Authority Key Identifier must not be empty.", nameof(authorityKeyIdentifier));
        }

        if (serialNumber.IsEmpty)
        {
            throw new ArgumentException("The DER-encoded certificate serial number must not be empty.", nameof(serialNumber));
        }

        return $"{Base64Url.EncodeToString(authorityKeyIdentifier)}.{Base64Url.EncodeToString(serialNumber)}";
    }

    public async Task<AcmeResult<AcmeOrderResource>> FinalizeOrderAsync(
        AcmeAccountHandle account,
        Uri finalizeUrl,
        ReadOnlyMemory<byte> certificateSigningRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(finalizeUrl);

        var request = new AcmeFinalizeOrderRequest
        {
            Csr = Base64Url.EncodeToString(certificateSigningRequest.Span)
        };

        var response = await SendSignedRequestAsync(
            finalizeUrl,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(request),
            cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeOrderResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeAuthorizationResource>> CreateAuthorizationAsync(
        AcmeAccountHandle account,
        AcmeNewAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(request);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (directory.NewAuthorization is null)
        {
            throw new InvalidOperationException("The ACME server does not advertise the newAuthz resource.");
        }

        var response = await SendSignedRequestAsync(
            directory.NewAuthorization,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(request),
            cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeAuthorizationResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeAuthorizationResource>> GetAuthorizationAsync(
        AcmeAccountHandle account,
        Uri authorizationUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(authorizationUrl);

        var response = await SendPostAsGetAsync(account.Signer, account.AccountUrl, authorizationUrl, cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeAuthorizationResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeAuthorizationResource>> DeactivateAuthorizationAsync(
        AcmeAccountHandle account,
        Uri authorizationUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(authorizationUrl);

        var response = await SendSignedRequestAsync(
            authorizationUrl,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(new AcmeAuthorizationStatusUpdateRequest { Status = AcmeAuthorizationStatuses.Deactivated }),
            cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeAuthorizationResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeChallengeResource>> GetChallengeAsync(
        AcmeAccountHandle account,
        Uri challengeUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(challengeUrl);

        var response = await SendPostAsGetAsync(account.Signer, account.AccountUrl, challengeUrl, cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeChallengeResource>(response.Body));
    }

    public async Task<AcmeResult<AcmeChallengeResource>> AnswerChallengeAsync(
        AcmeAccountHandle account,
        Uri challengeUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(challengeUrl);

        var response = await SendSignedRequestAsync(
            challengeUrl,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(new AcmeEmptyObject()),
            cancellationToken).ConfigureAwait(false);

        return CreateResult(response, DeserializeUtf8<AcmeChallengeResource>(response.Body));
    }

    public async Task<AcmeCertificateChain> DownloadCertificateAsync(
        AcmeAccountHandle account,
        Uri certificateUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(certificateUrl);

        var response = await SendPostAsGetAsync(
            account.Signer,
            account.AccountUrl,
            certificateUrl,
            cancellationToken,
            PemCertificateChainMediaType).ConfigureAwait(false);
        EnsurePemCertificateChainResponse(response);
        var pem = Encoding.ASCII.GetString(response.Body.Span);
        var certificates = ParsePemCertificateChain(pem);

        return new AcmeCertificateChain
        {
            PemChain = pem,
            Certificates = certificates,
            AlternateCertificateUrls = response.Links.Where(x => string.Equals(x.Relation, "alternate", StringComparison.OrdinalIgnoreCase)).Select(x => x.Uri).ToArray(),
            IssuerCertificateUrls = response.Links.Where(x => string.Equals(x.Relation, "up", StringComparison.OrdinalIgnoreCase)).Select(x => x.Uri).ToArray()
        };
    }

    public async Task RevokeCertificateAsync(
        AcmeAccountHandle account,
        ReadOnlyMemory<byte> certificateDer,
        int? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (directory.RevokeCertificate is null)
        {
            throw new InvalidOperationException("The ACME server does not advertise the revokeCert resource.");
        }

        var request = new AcmeRevocationRequest
        {
            Certificate = Base64Url.EncodeToString(certificateDer.Span),
            Reason = reason
        };

        _ = await SendSignedRequestAsync(
            directory.RevokeCertificate,
            account.Signer,
            account.AccountUrl,
            SerializeUtf8(request),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeCertificateAsync(
        AcmeSigner certificateSigner,
        ReadOnlyMemory<byte> certificateDer,
        int? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificateSigner);

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);

        if (directory.RevokeCertificate is null)
        {
            throw new InvalidOperationException("The ACME server does not advertise the revokeCert resource.");
        }

        var request = new AcmeRevocationRequest
        {
            Certificate = Base64Url.EncodeToString(certificateDer.Span),
            Reason = reason
        };

        _ = await SendSignedRequestAsync(
            directory.RevokeCertificate,
            certificateSigner,
            keyId: null,
            SerializeUtf8(request),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<AcmeDirectoryResource> EnsureDirectoryAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!forceRefresh && _directory is not null)
        {
            return _directory;
        }

        await _directoryLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!forceRefresh && _directory is not null)
            {
                return _directory;
            }

            var response = await SendBootstrapRequestAsync(HttpMethod.Get, _directoryUrl, cancellationToken).ConfigureAwait(false);
            _directory = DeserializeUtf8<AcmeDirectoryResource>(response.Body);

            return _directory;
        }
        finally
        {
            _directoryLock.Release();
        }
    }

    private async Task<AcmeRawResponse> SendPostAsGetAsync(
        AcmeSigner signer,
        Uri accountUrl,
        Uri requestUrl,
        CancellationToken cancellationToken,
        string? accept = null)
    {
        return await SendSignedRequestAsync(
            requestUrl,
            signer,
            accountUrl,
            payload: ReadOnlyMemory<byte>.Empty,
            cancellationToken,
            accept).ConfigureAwait(false);
    }

    private async Task<AcmeRawResponse> SendSignedRequestAsync(
        Uri requestUrl,
        AcmeSigner signer,
        Uri? keyId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken,
        string? accept = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            var nonce = await GetNonceAsync(cancellationToken).ConfigureAwait(false);
            var message = CreateSignedMessage(requestUrl, signer, keyId, payload, nonce);
            var contentBytes = SerializeUtf8(message);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            request.Content = new ByteArrayContent(contentBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(JoseMediaType);

            if (!string.IsNullOrWhiteSpace(accept))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            }

            ApplyStandardHeaders(request);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var rawResponse = await ToRawResponseAsync(response, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return rawResponse;
            }

            var exception = CreateProtocolException(rawResponse, requestUrl);

            if (exception.IsBadNonce && attempt < _options.BadNonceRetryCount)
            {
                continue;
            }

            throw exception;
        }
    }

    private async Task<string> GetNonceAsync(CancellationToken cancellationToken)
    {
        if (_nonceStore.TryTake(out var existingNonce))
        {
            return existingNonce;
        }

        var directory = await EnsureDirectoryAsync(forceRefresh: false, cancellationToken).ConfigureAwait(false);
        var response = await SendBootstrapRequestAsync(HttpMethod.Head, directory.NewNonce, cancellationToken).ConfigureAwait(false);

        return response.ReplayNonce ?? throw new AcmeProtocolException(
            response.StatusCode,
            "The ACME server did not provide a Replay-Nonce header.",
            directory.NewNonce,
            replayNonce: response.ReplayNonce,
            retryAfter: response.RetryAfter,
            links: response.Links);
    }

    private async Task<AcmeRawResponse> SendBootstrapRequestAsync(HttpMethod method, Uri requestUrl, CancellationToken cancellationToken, string? accept = null)
    {
        ThrowIfDisposed();

        using var request = new HttpRequestMessage(method, requestUrl);

        if (!string.IsNullOrWhiteSpace(accept))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        ApplyStandardHeaders(request);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var rawResponse = await ToRawResponseAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return rawResponse;
        }

        throw CreateProtocolException(rawResponse, requestUrl);
    }

    private AcmeSignedMessage CreateSignedMessage(Uri requestUrl, AcmeSigner signer, Uri? keyId, ReadOnlyMemory<byte> payload, string? nonce)
    {
        var protectedHeader = new AcmeProtectedHeader
        {
            Algorithm = signer.Algorithm,
            JsonWebKey = keyId is null ? signer.ExportJsonWebKey() : null,
            KeyIdentifier = keyId?.OriginalString,
            Nonce = nonce,
            Url = requestUrl.OriginalString
        };

        var protectedHeaderBytes = SerializeUtf8(protectedHeader);
        var protectedHeaderEncoded = Base64Url.EncodeToString(protectedHeaderBytes);
        var payloadEncoded = payload.Length == 0 ? string.Empty : Base64Url.EncodeToString(payload.Span);
        var signingInput = Encoding.ASCII.GetBytes($"{protectedHeaderEncoded}.{payloadEncoded}");
        var signature = signer.SignData(signingInput);

        return new AcmeSignedMessage
        {
            Protected = protectedHeaderEncoded,
            Payload = payloadEncoded,
            Signature = Base64Url.EncodeToString(signature)
        };
    }

    private static AcmeSignedMessage CreateExternalAccountBinding(
        Uri requestUrl,
        AcmeExternalAccountBindingOptions options,
        AcmeSigner signer)
    {
        var protectedHeader = new AcmeExternalAccountProtectedHeader
        {
            Algorithm = options.Algorithm,
            KeyIdentifier = options.KeyIdentifier,
            Url = requestUrl.OriginalString
        };

        var payload = SerializeUtf8(signer.ExportJsonWebKey());
        var protectedHeaderBytes = SerializeUtf8(protectedHeader);
        var protectedHeaderEncoded = Base64Url.EncodeToString(protectedHeaderBytes);
        var payloadEncoded = Base64Url.EncodeToString(payload);
        var signingInput = Encoding.ASCII.GetBytes($"{protectedHeaderEncoded}.{payloadEncoded}");
        var signature = ComputeHmac(options.Algorithm, options.HmacKey.Span, signingInput);

        return new AcmeSignedMessage
        {
            Protected = protectedHeaderEncoded,
            Payload = payloadEncoded,
            Signature = Base64Url.EncodeToString(signature)
        };
    }

    private void ApplyStandardHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

        if (!string.IsNullOrWhiteSpace(_options.AcceptLanguage))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", _options.AcceptLanguage);
        }
    }

    private async Task<AcmeRawResponse> ToRawResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var replayNonce = TryGetSingleHeaderValue(response.Headers, "Replay-Nonce");

        if (!string.IsNullOrWhiteSpace(replayNonce))
        {
            _nonceStore.Add(replayNonce);
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        return new AcmeRawResponse
        {
            StatusCode = response.StatusCode,
            Location = response.Headers.Location,
            ReplayNonce = replayNonce,
            RetryAfter = TryGetRetryAfter(response.Headers),
            Links = AcmeHeaderParser.ParseLinkHeaders(response.Headers),
            ContentType = response.Content.Headers.ContentType,
            Body = body
        };
    }

    private static void EnsurePemCertificateChainResponse(AcmeRawResponse response)
    {
        var contentType = response.ContentType?.MediaType;

        if (contentType is not null && !string.Equals(contentType, PemCertificateChainMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The ACME server returned '{contentType}' instead of '{PemCertificateChainMediaType}' for the certificate chain.");
        }
    }

    private static IReadOnlyList<X509Certificate2> ParsePemCertificateChain(string pem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pem);

        var certificates = new List<X509Certificate2>();
        var remaining = pem.AsSpan();

        while (!remaining.IsEmpty)
        {
            if (!PemEncoding.TryFind(remaining, out var fields))
            {
                if (remaining.Trim().Length == 0)
                {
                    break;
                }

                throw new InvalidOperationException("The PEM response contains invalid data.");
            }

            var label = remaining[fields.Label].ToString();

            if (!string.Equals(label, "CERTIFICATE", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The PEM response contains data other than certificates.");
            }

            var decodedDataLength = fields.DecodedDataLength;
            var der = new byte[decodedDataLength];

            if (!Convert.TryFromBase64Chars(remaining[fields.Base64Data], der, out _))
            {
                throw new InvalidOperationException("The PEM response contains invalid base64 data.");
            }

            certificates.Add(X509CertificateLoader.LoadCertificate(der));
            remaining = remaining[fields.Location.End.Value..];
        }

        if (certificates.Count == 0)
        {
            throw new InvalidOperationException("The PEM response did not contain any certificates.");
        }

        return certificates;
    }

    private static AcmeAccountHandle CreateAccountHandle(Uri accountUrl, AcmeSigner signer, AcmeAccountResource resource)
    {
        return new AcmeAccountHandle
        {
            AccountUrl = accountUrl,
            Signer = signer,
            Account = resource
        };
    }

    private static AcmeAccountHandle CreateAccountHandle(AcmeAccountHandle account, AcmeAccountResource resource, AcmeSigner? signer = null)
    {
        return CreateAccountHandle(account.AccountUrl, signer ?? account.Signer, resource);
    }

    private static AcmeResult<T> CreateResult<T>(AcmeRawResponse response, T resource)
    {
        return new AcmeResult<T>
        {
            Resource = resource,
            Location = response.Location,
            RetryAfter = response.RetryAfter,
            Links = response.Links
        };
    }

    private static AcmeProtocolException CreateProtocolException(AcmeRawResponse rawResponse, Uri requestUrl)
    {
        var problem = TryDeserializeUtf8<AcmeProblemDetails>(rawResponse.Body);

        return new AcmeProtocolException(
            rawResponse.StatusCode,
            problem?.Detail ?? $"The ACME server returned {(int)rawResponse.StatusCode} ({rawResponse.StatusCode}).",
            requestUrl,
            problem,
            rawResponse.ReplayNonce,
            rawResponse.RetryAfter,
            rawResponse.Links);
    }

    private static byte[] SerializeUtf8<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, s_jsonSerializerOptions);
    }

    private static T DeserializeUtf8<T>(ReadOnlyMemory<byte> data)
    {
        var value = JsonSerializer.Deserialize<T>(data.Span, s_jsonSerializerOptions);

        return value ?? throw new InvalidOperationException($"Unable to deserialize the ACME response as {typeof(T).Name}.");
    }

    private static T? TryDeserializeUtf8<T>(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(data.Span, s_jsonSerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? TryGetSingleHeaderValue(HttpResponseHeaders headers, string name)
    {
        return headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }

    private static TimeSpan? TryGetRetryAfter(HttpResponseHeaders headers)
    {
        var retryAfter = headers.RetryAfter;

        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is not null)
        {
            return retryAfter.Delta;
        }

        if (retryAfter.Date is not null)
        {
            var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        return null;
    }

    private static byte[] ComputeHmac(string algorithm, ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        return algorithm switch
        {
            "HS256" => HMACSHA256.HashData(key, data),
            "HS384" => HMACSHA384.HashData(key, data),
            "HS512" => HMACSHA512.HashData(key, data),
            _ => throw new NotSupportedException("Only HS256, HS384, and HS512 external account binding algorithms are supported.")
        };
    }

    private static Uri CreateResourceUrl(Uri baseUrl, string relativePath)
    {
        return new Uri($"{baseUrl.AbsoluteUri.TrimEnd('/')}/{Uri.EscapeDataString(relativePath)}", UriKind.Absolute);
    }

    private static byte[] GetAuthorityKeyIdentifier(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions["2.5.29.35"]
            ?? throw new InvalidOperationException("The certificate does not contain an Authority Key Identifier extension.");

        var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        byte[]? authorityKeyIdentifier = null;

        while (sequence.HasData)
        {
            if (sequence.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0)))
            {
                authorityKeyIdentifier = sequence.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0));
                break;
            }

            _ = sequence.ReadEncodedValue();
        }

        reader.ThrowIfNotEmpty();

        return authorityKeyIdentifier is { Length: > 0 }
            ? authorityKeyIdentifier
            : throw new InvalidOperationException("The certificate Authority Key Identifier extension does not contain a keyIdentifier value.");
    }

    private static byte[] GetDerEncodedSerialNumber(ReadOnlyMemory<byte> certificateDer)
    {
        var reader = new AsnReader(certificateDer, AsnEncodingRules.DER);
        var certificate = reader.ReadSequence();
        var tbsCertificate = certificate.ReadSequence();

        if (tbsCertificate.HasData && tbsCertificate.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true)))
        {
            var version = tbsCertificate.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
            _ = version.ReadInteger();
            version.ThrowIfNotEmpty();
        }

        var serialNumber = tbsCertificate.ReadIntegerBytes().ToArray();
        reader.ThrowIfNotEmpty();

        return serialNumber.Length > 0
            ? serialNumber
            : throw new InvalidOperationException("The certificate serial number is missing.");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class AcmeRawResponse
    {
        public required HttpStatusCode StatusCode { get; init; }

        public required ReadOnlyMemory<byte> Body { get; init; }

        public string? ReplayNonce { get; init; }

        public Uri? Location { get; init; }

        public TimeSpan? RetryAfter { get; init; }

        public IReadOnlyList<AcmeLinkHeader> Links { get; init; } = [];

        public MediaTypeHeaderValue? ContentType { get; init; }
    }
}
