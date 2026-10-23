using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Options;
using Acmebot.App.Services;

using Azure;
using Azure.Security.KeyVault.Certificates;

using Microsoft.Extensions.Options;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateActivitiesTests
{
    [Theory]
    [InlineData("expired", 0)]
    [InlineData("local", 0)]
    [InlineData("ari", 2)]
    [InlineData("ari-failure", 2)]
    [InlineData("ari-unsupported", 1)]
    public async Task EvaluateCertificateRenewal_WhenDue_ReturnsPolicyFromSingleRead(string scenario, int expectedRequests)
    {
        using var handler = new RenewalInfoHandler(scenario);
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        using var factory = CreateFactory(options, httpClient);
        var certificateId = scenario == "local" ? null : "certificate-id";
        var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddDays(scenario == "expired" ? -1 : 10), certificateId);
        var client = new RecordingCertificateClient(certificate);

        var evaluation = await CreateActivities(factory, client, options).EvaluateCertificateRenewal("example-com");

        Assert.True(evaluation.IsActive);
        Assert.True(evaluation.ShouldRenew);
        Assert.Equal(1, client.GetCount);
        Assert.Equal(expectedRequests, handler.RequestCount);
        var policy = Assert.IsType<Acmebot.App.Models.CertificatePolicyItem>(evaluation.CertificatePolicy);
        Assert.Equal("example-com", policy.CertificateName);
        Assert.Equal(new[] { "example.com", "*.example.com" }, policy.DnsNames);
        Assert.Equal("Azure DNS", policy.DnsProviderName);
        Assert.Equal("RSA", policy.KeyType);
        Assert.Equal(2048, policy.KeySize);
        Assert.True(policy.ReuseKey);
        Assert.Equal("validation.example.com", policy.DnsAlias);
        Assert.Equal("tlsserver", policy.Profile);
        Assert.Equal(certificateId, policy.CertificateId);
        Assert.Equal("production", policy.Tags!["environment"]);
        Assert.DoesNotContain("Acmebot", policy.Tags.Keys);

        certificate.Properties.Tags["environment"] = "changed";
        certificate.Policy.SubjectAlternativeNames.DnsNames.Add("changed.example.com");
        Assert.Equal("production", policy.Tags["environment"]);
        Assert.Equal(2, policy.DnsNames.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvaluateCertificateRenewal_WhenNotDue_DoesNotConvertPolicy(bool useAri)
    {
        using var handler = new RenewalInfoHandler("ari") { FutureWindow = true };
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        using var factory = CreateFactory(options, httpClient);
        // A missing policy would fail conversion, but it is not needed until renewal is due.
        var client = new RecordingCertificateClient(CreateCertificate(DateTimeOffset.UtcNow.AddDays(90), useAri ? "certificate-id" : null, includePolicy: false));

        var evaluation = await CreateActivities(factory, client, options).EvaluateCertificateRenewal("example-com");

        Assert.True(evaluation.IsActive);
        Assert.False(evaluation.ShouldRenew);
        Assert.Null(evaluation.CertificatePolicy);
        Assert.Equal(1, client.GetCount);
        Assert.Equal(useAri ? 2 : 0, handler.RequestCount);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("unmanaged")]
    [InlineData("other-endpoint")]
    public async Task EvaluateCertificateRenewal_WhenInactive_DoesNotConvertPolicy(string scenario)
    {
        using var handler = new RenewalInfoHandler("ari");
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        using var factory = CreateFactory(options, httpClient);
        var certificate = CreateCertificate(DateTimeOffset.UtcNow.AddDays(-1), includePolicy: false);
        if (scenario == "disabled")
        {
            certificate.Properties.Enabled = false;
        }
        else if (scenario == "unmanaged")
        {
            certificate.Properties.Tags.Clear();
        }
        else
        {
            certificate.Properties.Tags["Acmebot"] = """{"endpoint":"other.example"}""";
        }
        var client = new RecordingCertificateClient(certificate);

        var evaluation = await CreateActivities(factory, client, options).EvaluateCertificateRenewal("example-com");

        Assert.False(evaluation.IsActive);
        Assert.False(evaluation.ShouldRenew);
        Assert.Null(evaluation.CertificatePolicy);
        Assert.Equal(1, client.GetCount);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task EvaluateCertificateRenewal_WhenMissing_StopsWithoutPolicy()
    {
        using var handler = new RenewalInfoHandler("ari");
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        using var factory = CreateFactory(options, httpClient);
        var client = new RecordingCertificateClient(CreateCertificate(DateTimeOffset.UtcNow.AddDays(-1)))
        {
            Failure = new RequestFailedException(404, "Not found.")
        };

        var evaluation = await CreateActivities(factory, client, options).EvaluateCertificateRenewal("example-com");

        Assert.False(evaluation.IsActive);
        Assert.False(evaluation.ShouldRenew);
        Assert.Null(evaluation.CertificatePolicy);
        Assert.Equal("Certificate was not found.", evaluation.Reason);
        Assert.Equal(1, client.GetCount);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task EvaluateCertificateRenewal_WhenPolicyConversionFails_DoesNotTreatItAsAriFailure()
    {
        using var handler = new RenewalInfoHandler("ari");
        using var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        using var factory = CreateFactory(options, httpClient);
        var client = new RecordingCertificateClient(CreateCertificate(DateTimeOffset.UtcNow.AddDays(10), "certificate-id", includePolicy: false));

        await Assert.ThrowsAsync<NullReferenceException>(() => CreateActivities(factory, client, options).EvaluateCertificateRenewal("example-com"));

        Assert.Equal(1, client.GetCount);
        Assert.Equal(2, handler.RequestCount);
    }

    private static IOptions<AcmebotOptions> CreateOptions() => Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
    {
        Endpoint = new Uri("https://acme.example/directory"),
        Contacts = "admin@example.com",
        VaultBaseUrl = "https://vault.example/",
        RenewBeforeExpiry = 20
    });

    private static AcmeClientFactory CreateFactory(IOptions<AcmebotOptions> options, HttpClient httpClient)
        => new(options, new ExistingAccountStore(), () => new AcmeClient(httpClient, options.Value.Endpoint));

    private static CertificateActivities CreateActivities(AcmeClientFactory factory, CertificateClient client, IOptions<AcmebotOptions> options)
        => new(factory, client, new CertificateOperationService(factory, client), options);

    private static KeyVaultCertificateWithPolicy CreateCertificate(DateTimeOffset expiresOn, string? certificateId = null, bool includePolicy = true)
    {
        var names = new SubjectAlternativeNames();
        names.DnsNames.Add("example.com");
        names.DnsNames.Add("*.example.com");
        var policy = new CertificatePolicy(WellKnownIssuerNames.Unknown, "CN=example.com", names)
        {
            KeyType = CertificateKeyType.Rsa,
            KeySize = 2048,
            ReuseKey = true
        };
        var properties = CertificateModelFactory.CertificateProperties(
            id: new Uri("https://vault.example/certificates/example-com/version"),
            name: "example-com",
            notBefore: DateTimeOffset.UtcNow.AddDays(-80),
            expiresOn: expiresOn,
            createdOn: DateTimeOffset.UtcNow.AddDays(-80));
        properties.Tags["Acmebot"] = JsonSerializer.Serialize(new
        {
            endpoint = "acme.example",
            dnsProvider = "Azure DNS",
            dnsAlias = "validation.example.com",
            profile = "tlsserver",
            certificateId
        });
        properties.Tags["environment"] = "production";

        return CertificateModelFactory.KeyVaultCertificateWithPolicy(properties, null, null, [], includePolicy ? policy : null);
    }

    private sealed class RecordingCertificateClient(KeyVaultCertificateWithPolicy certificate) : CertificateClient
    {
        public int GetCount { get; private set; }
        public Exception? Failure { get; init; }

        public override Task<Response<KeyVaultCertificateWithPolicy>> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
        {
            Assert.Equal("example-com", certificateName);
            GetCount++;
            return Failure is { } failure
                ? Task.FromException<Response<KeyVaultCertificateWithPolicy>>(failure)
                : Task.FromResult(Response.FromValue(certificate, null!));
        }
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

    private sealed class RenewalInfoHandler(string scenario) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool FutureWindow { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.RequestUri!.AbsolutePath == "/directory")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        newNonce = "https://acme.example/nonce",
                        newAccount = "https://acme.example/account",
                        newOrder = "https://acme.example/order",
                        renewalInfo = scenario == "ari-unsupported" ? null : "https://acme.example/renewal-info"
                    })
                });
            }

            Assert.Equal("/renewal-info/certificate-id", request.RequestUri.AbsolutePath);
            var start = DateTimeOffset.UtcNow.AddDays(FutureWindow ? 1 : -2);
            return Task.FromResult(new HttpResponseMessage(scenario == "ari-failure" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { suggestedWindow = new { start, end = start.AddDays(1) } })
            });
        }
    }
}
