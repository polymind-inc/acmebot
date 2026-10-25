using System.Security.Claims;
using System.Text.Json;

using Acmebot.App.Acme;
using Acmebot.App.Functions.Http;
using Acmebot.App.Models;
using Acmebot.App.Options;
using Acmebot.App.Services;

using Azure;
using Azure.Security.KeyVault.Certificates;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Acmebot.App.Tests;

[Collection(AppServiceAuthenticationCollection.Name)]
public sealed class CertificateOwnershipTests
{
    [Theory]
    [InlineData(CertificateState.Missing, null)]
    [InlineData(CertificateState.Managed, null)]
    [InlineData(CertificateState.OtherEndpoint, null)]
    [InlineData(CertificateState.Unmanaged, "true")]
    [InlineData(CertificateState.Unmanaged, "True")]
    public async Task AddCertificate_WhenReplacementIsAllowed_SchedulesIssuance(CertificateState state, string? replaceExisting)
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = CreateHttpContext(replaceExisting);
        var starter = new RecordingDurableTaskClient();

        await Assert.ThrowsAsync<ReachedOperationException>(() => CreateAddCertificate(context, state).HttpStart(context.Request, CreatePolicy(), starter));

        Assert.Equal("example-com", Assert.IsType<CertificatePolicyItem>(starter.Input).CertificateName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("yes")]
    public async Task AddCertificate_WhenUnmanagedCertificateExistsWithoutOptIn_ReturnsConflict(string? replaceExisting)
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = CreateHttpContext(replaceExisting);
        var starter = new RecordingDurableTaskClient();

        var result = await CreateAddCertificate(context, CertificateState.Unmanaged).HttpStart(context.Request, CreatePolicy(), starter);

        AssertProblem(result, StatusCodes.Status409Conflict, "replaceExisting=true");
        Assert.Null(starter.Input);
    }

    [Theory]
    [InlineData(CertificateState.Managed)]
    [InlineData(CertificateState.OtherEndpoint)]
    public async Task RenewCertificate_WhenManagedByAcmebot_SchedulesIssuanceWithoutCertificateId(CertificateState state)
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = CreateHttpContext();
        var starter = new RecordingDurableTaskClient();

        await Assert.ThrowsAsync<ReachedOperationException>(() => CreateRenewCertificate(context, state).HttpStart(context.Request, "example-com", starter));

        var policy = Assert.IsType<CertificatePolicyItem>(starter.Input);
        Assert.Equal("example-com", policy.CertificateName);
        Assert.Equal("Azure DNS", policy.DnsProviderName);
        Assert.Null(policy.CertificateId);
    }

    [Theory]
    [InlineData(CertificateState.Missing, StatusCodes.Status404NotFound)]
    [InlineData(CertificateState.Unmanaged, StatusCodes.Status409Conflict)]
    public async Task RenewCertificate_WhenNotManagedByAcmebot_ReturnsProblem(CertificateState state, int expectedStatusCode)
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = CreateHttpContext();
        var starter = new RecordingDurableTaskClient();

        var result = await CreateRenewCertificate(context, state).HttpStart(context.Request, "example-com", starter);

        AssertProblem(result, expectedStatusCode, "example-com");
        Assert.Null(starter.Input);
    }

    [Fact]
    public async Task RevokeCertificate_WhenIssuedByCurrentEndpoint_RevokesCertificate()
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = CreateHttpContext();

        // The ACME state store throws once revocation starts, proving the ownership checks passed.
        await Assert.ThrowsAsync<ReachedOperationException>(() => CreateRevokeCertificate(context, CertificateState.Managed).HttpStart(context.Request, "example-com"));
    }

    [Theory]
    [InlineData(CertificateState.Missing, StatusCodes.Status404NotFound)]
    [InlineData(CertificateState.Unmanaged, StatusCodes.Status409Conflict)]
    [InlineData(CertificateState.OtherEndpoint, StatusCodes.Status409Conflict)]
    public async Task RevokeCertificate_WhenNotIssuedByCurrentEndpoint_ReturnsProblem(CertificateState state, int expectedStatusCode)
    {
        using var authentication = new AppServiceAuthenticationScope("true");
        var context = CreateHttpContext();

        var result = await CreateRevokeCertificate(context, state).HttpStart(context.Request, "example-com");

        AssertProblem(result, expectedStatusCode, "example-com");
    }

    public enum CertificateState
    {
        Missing,
        Managed,
        OtherEndpoint,
        Unmanaged
    }

    private static readonly IOptions<AcmebotOptions> s_options = Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
    {
        Contacts = "admin@example.com",
        Endpoint = new Uri("https://acme.example/directory"),
        VaultBaseUrl = "https://vault.example/"
    });

    private static readonly IServiceProvider s_requestServices = new ServiceCollection()
        .AddLogging()
        .AddMvcCore()
        .Services
        .BuildServiceProvider();

    private static AddCertificate CreateAddCertificate(HttpContext context, CertificateState state)
        => new(new HttpContextAccessor { HttpContext = context }, new AppRoleService(s_options), CreateOperationService(state), NullLogger<AddCertificate>.Instance);

    private static RenewCertificate CreateRenewCertificate(HttpContext context, CertificateState state)
        => new(new HttpContextAccessor { HttpContext = context }, new AppRoleService(s_options), CreateOperationService(state), NullLogger<RenewCertificate>.Instance);

    private static RevokeCertificate CreateRevokeCertificate(HttpContext context, CertificateState state)
        => new(new HttpContextAccessor { HttpContext = context }, new AppRoleService(s_options), CreateOperationService(state), s_options, NullLogger<RevokeCertificate>.Instance);

    private static CertificateOperationService CreateOperationService(CertificateState state)
        => new(new AcmeClientFactory(s_options, new ThrowingStateStore()), new FakeCertificateClient(state == CertificateState.Missing ? null : CreateCertificate(state)));

    private static DefaultHttpContext CreateHttpContext(string? replaceExisting = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@example.com")], "aad")),
            // Model validation and problem responses resolve their MVC services from the request.
            RequestServices = s_requestServices
        };

        if (replaceExisting is not null)
        {
            context.Request.QueryString = QueryString.Create("replaceExisting", replaceExisting);
        }

        return context;
    }

    private static CertificatePolicyItem CreatePolicy() => new()
    {
        CertificateName = "example-com",
        DnsNames = ["example.com"],
        DnsProviderName = "Azure DNS",
        KeyType = "RSA",
        KeySize = 2048
    };

    private static KeyVaultCertificateWithPolicy CreateCertificate(CertificateState state)
    {
        var names = new SubjectAlternativeNames();
        names.DnsNames.Add("example.com");
        var policy = new CertificatePolicy(WellKnownIssuerNames.Unknown, "CN=example.com", names)
        {
            KeyType = CertificateKeyType.Rsa,
            KeySize = 2048
        };
        var properties = CertificateModelFactory.CertificateProperties(
            id: new Uri("https://vault.example/certificates/example-com/version"),
            name: "example-com");

        if (state != CertificateState.Unmanaged)
        {
            properties.Tags["Acmebot"] = JsonSerializer.Serialize(new
            {
                endpoint = state == CertificateState.Managed ? "acme.example" : "other.example",
                dnsProvider = "Azure DNS",
                certificateId = "certificate-id"
            });
        }

        return CertificateModelFactory.KeyVaultCertificateWithPolicy(properties, null, null, [], policy);
    }

    private static void AssertProblem(IActionResult result, int expectedStatusCode, string expectedDetail)
    {
        var objectResult = Assert.IsType<ObjectResult>(result, exactMatch: false);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        Assert.Contains(expectedDetail, problem.Detail);
    }

    private sealed class ReachedOperationException : Exception;

    private sealed class FakeCertificateClient(KeyVaultCertificateWithPolicy? certificate) : CertificateClient
    {
        public override Task<Response<KeyVaultCertificateWithPolicy>> GetCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
        {
            Assert.Equal("example-com", certificateName);

            return certificate is null
                ? Task.FromException<Response<KeyVaultCertificateWithPolicy>>(new RequestFailedException(404, "Not found"))
                : Task.FromResult(Response.FromValue(certificate, null!));
        }
    }

    private sealed class ThrowingStateStore : IAcmeStateStore
    {
        public Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default) => throw new ReachedOperationException();
        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default) => throw new ReachedOperationException();
        public Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => throw new ReachedOperationException();
        public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => throw new ReachedOperationException();
    }

    private sealed class RecordingDurableTaskClient() : DurableTaskClient("test")
    {
        public object? Input { get; private set; }

        // Record the input and stop before the function builds its status URL, which needs routing.
        public override Task<string> ScheduleNewOrchestrationInstanceAsync(TaskName orchestratorName, object? input = null, StartOrchestrationOptions? options = null, CancellationToken cancellation = default)
        {
            Input = input;

            throw new ReachedOperationException();
        }

        public override Task RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null, CancellationToken cancellation = default) => throw new NotSupportedException();
        public override Task<OrchestrationMetadata> WaitForInstanceStartAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default) => throw new NotSupportedException();
        public override Task<OrchestrationMetadata> WaitForInstanceCompletionAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default) => throw new NotSupportedException();
        public override Task SuspendInstanceAsync(string instanceId, string? reason = null, CancellationToken cancellation = default) => throw new NotSupportedException();
        public override Task ResumeInstanceAsync(string instanceId, string? reason = null, CancellationToken cancellation = default) => throw new NotSupportedException();
        public override Task<OrchestrationMetadata?> GetInstancesAsync(string instanceId, bool getInputsAndOutputs = false, CancellationToken cancellation = default) => throw new NotSupportedException();
        public override Microsoft.DurableTask.AsyncPageable<OrchestrationMetadata> GetAllInstancesAsync(OrchestrationQuery? filter = null) => throw new NotSupportedException();
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
