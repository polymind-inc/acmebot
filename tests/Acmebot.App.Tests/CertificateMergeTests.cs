using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;
using Acmebot.App.Options;

using Azure;
using Azure.Security.KeyVault.Certificates;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateMergeTests
{
    [Theory]
    [InlineData(true, false, "", null, "Azure DNS", "validation.example.net", 5)]
    [InlineData(false, true, "Azure DNS", "old.example.net", "", null, 5)]
    [InlineData(false, false, "Azure DNS", "old.example.net", "Custom DNS", "new.example.net", 5)]
    [InlineData(false, false, "Custom DNS", "old.example.net", "Azure DNS", null, 5)]
    [InlineData(false, true, "Azure DNS", "old.example.net", "Custom DNS", null, 3)]
    [InlineData(true, false, "Azure DNS", "old.example.net", "Custom DNS", null, 3)]
    [InlineData(false, null, "Azure DNS", "old.example.net", "Custom DNS", null, 2)]
    [InlineData(true, null, "", null, "Azure DNS", "validation.example.net", 2)]
    public async Task MergeCertificate_AfterReusingPendingOperation_PersistsRequestedSettingsOrPreservesLegacySettings(
        bool savedMode, bool? requestedMode, string savedProvider, string? savedAlias, string requestedProvider, string? requestedAlias, int inputVersion)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=example.com", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectKeyIdentifier = new X509SubjectKeyIdentifierExtension(request.PublicKey, false);
        request.CertificateExtensions.Add(subjectKeyIdentifier);
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(subjectKeyIdentifier));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var csr = request.CreateSigningRequest();
        var endpoint = new Uri("https://acme.example/directory");
        var policy = new CertificatePolicyItem
        {
            CertificateName = "example-com",
            DnsNames = ["example.com"],
            DnsProviderName = savedProvider,
            DnsAlias = savedAlias,
            KeyType = "RSA",
            KeySize = 2048,
            UsePrevalidatedAuthorization = savedMode,
            Profile = "tlsserver",
            Tags = new Dictionary<string, string> { ["owner"] = "platform" }
        };
        var properties = CertificateModelFactory.CertificateProperties(
            id: new Uri("https://vault.example/certificates/example-com/version"), name: "example-com", x509thumbprint: certificate.GetCertHash());
        foreach (var tag in policy.ToCertificateTags(endpoint))
        {
            properties.Tags[tag.Key] = tag.Value;
        }
        var storedCertificate = CertificateModelFactory.KeyVaultCertificateWithPolicy(properties, null, null, certificate.RawData, policy.ToCertificatePolicy());
        var vaultClient = new PendingCertificateClient(storedCertificate, csr);
        using var handler = new CertificateHandler(certificate.ExportCertificatePem(), csr);
        using var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            Contacts = "admin@example.com",
            Endpoint = endpoint,
            VaultBaseUrl = "https://vault.example/"
        });
        using var factory = new AcmeClientFactory(options, new ExistingAccountStore(), () => new AcmeClient(httpClient, endpoint));
        var activities = new AcmeOrderActivities(factory, vaultClient, options, NullLogger<AcmeOrderActivities>.Instance);
        policy.UsePrevalidatedAuthorization = requestedMode ?? savedMode;
        policy.DnsProviderName = requestedProvider;
        policy.DnsAlias = requestedAlias;
        var order = new OrderDetails
        {
            OrderUrl = new Uri("https://acme.example/order/1"),
            Payload = new AcmeOrderResource { Status = AcmeOrderStatuses.Ready, Finalize = new Uri("https://acme.example/finalize/1") }
        };

        var finalized = await activities.FinalizeOrder((policy, order));
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        // Cover both earlier durable input formats as well as the current DNS settings snapshot.
        var inputJson = inputVersion switch
        {
            2 => JsonSerializer.Serialize((policy.CertificateName, finalized), jsonOptions),
            3 => JsonSerializer.Serialize((policy.CertificateName, finalized, requestedMode), jsonOptions),
            _ => JsonSerializer.Serialize((policy.CertificateName, finalized, requestedMode, requestedProvider, requestedAlias), jsonOptions)
        };
        var input = JsonSerializer.Deserialize<(string, OrderDetails, bool?, string?, string?)>(inputJson, jsonOptions);
        var result = await activities.MergeCertificate(input);

        Assert.Equal(1, vaultClient.ReusedOperationCount);
        Assert.Equal(1, vaultClient.MergeCount);
        Assert.Equal(1, vaultClient.UpdateCount);
        Assert.Equal(requestedMode ?? savedMode, result.UsePrevalidatedAuthorization);
        var expectedProvider = inputVersion == 5 ? requestedProvider : savedProvider;
        var expectedAlias = (inputVersion == 5 ? requestedAlias : savedAlias) ?? "";
        Assert.Equal(expectedProvider, result.DnsProviderName);
        Assert.Equal(expectedAlias, result.DnsAlias);
        var renewalPolicy = storedCertificate.ToCertificatePolicyItem();
        Assert.Equal(requestedMode ?? savedMode, renewalPolicy.UsePrevalidatedAuthorization);
        Assert.Equal(AcmeClient.CreateCertificateIdentifier(certificate), renewalPolicy.CertificateId);
        Assert.Equal(expectedProvider, renewalPolicy.DnsProviderName);
        Assert.Equal(expectedAlias, renewalPolicy.DnsAlias);
        Assert.Equal("tlsserver", renewalPolicy.Profile);
        Assert.Equal("platform", renewalPolicy.Tags!["owner"]);
        Assert.True(storedCertificate.Properties.IsSameEndpoint(endpoint));
    }

    private sealed class PendingCertificateClient(KeyVaultCertificateWithPolicy certificate, byte[] csr) : CertificateClient
    {
        public int ReusedOperationCount { get; private set; }
        public int MergeCount { get; private set; }
        public int UpdateCount { get; private set; }

        public override Task<Response<KeyVaultCertificateWithPolicy>> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
            => Task.FromResult(Response.FromValue(certificate, null!));

        public override Task<CertificateOperation> StartCreateCertificateAsync(string certificateName, CertificatePolicy policy, bool? enabled = null, IDictionary<string, string>? tags = null, bool? preserveCertificateOrder = null, CancellationToken cancellationToken = default)
            => throw new RequestFailedException(409, "A pending operation already exists.");

        public override Task<CertificateOperation> GetCertificateOperationAsync(string certificateName, CancellationToken cancellationToken = default)
        {
            ReusedOperationCount++;
            return Task.FromResult<CertificateOperation>(new PendingOperation(csr));
        }

        public override Task<Response<KeyVaultCertificateWithPolicy>> MergeCertificateAsync(MergeCertificateOptions options, CancellationToken cancellationToken = default)
        {
            MergeCount++;
            Assert.Equal(certificate.Cer, Assert.Single(options.X509Certificates));
            return Task.FromResult(Response.FromValue(certificate, null!));
        }

        public override Task<Response<KeyVaultCertificate>> UpdateCertificatePropertiesAsync(CertificateProperties properties, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            Assert.Same(certificate.Properties, properties);
            return Task.FromResult(Response.FromValue<KeyVaultCertificate>(certificate, null!));
        }
    }

    private sealed class PendingOperation(byte[] csr) : CertificateOperation
    {
        public override CertificateOperationProperties Properties { get; } = CertificateModelFactory.CertificateOperationProperties(csr: csr);
    }

    private sealed class ExistingAccountStore : IAcmeStateStore
    {
        private readonly AccountKey _key = AccountKey.CreateDefault();

        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default)
        {
            object state = path == "account_key.json" ? _key : new AccountDetails
            {
                Kid = new Uri("https://acme.example/account/1"),
                Payload = new AcmeAccountResource { Status = AcmeAccountStatuses.Valid }
            };
            return Task.FromResult((TState?)state);
        }

        public Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CertificateHandler(string pem, byte[] csr) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Replay-Nonce", Base64Url.EncodeToString(Guid.NewGuid().ToByteArray()));
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/directory":
                    response.Content = JsonContent.Create(new
                    {
                        newNonce = "https://acme.example/nonce",
                        newAccount = "https://acme.example/account",
                        newOrder = "https://acme.example/order"
                    });
                    break;
                case "/nonce":
                    break;
                case "/finalize/1":
                    var signedMessage = JsonSerializer.Deserialize<AcmeSignedMessage>(await request.Content!.ReadAsStringAsync(cancellationToken))!;
                    using (var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(signedMessage.Payload)))
                    {
                        Assert.Equal(Base64Url.EncodeToString(csr), payload.RootElement.GetProperty("csr").GetString());
                    }
                    response.Content = JsonContent.Create(new { status = "valid", certificate = "https://acme.example/certificate/1" });
                    break;
                case "/certificate/1":
                    response.Content = new StringContent(pem, Encoding.ASCII, "application/pem-certificate-chain");
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected ACME request: {request.RequestUri}");
            }
            return response;
        }
    }
}
