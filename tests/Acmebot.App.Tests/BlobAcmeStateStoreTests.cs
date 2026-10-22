using System.Net;

using Acmebot.App.Acme;
using Acmebot.App.Options;

using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class BlobAcmeStateStoreTests
{
    [Fact]
    public async Task AcquireLockAsync_IndependentStoresWaitForLeaseAndReuseLockBlob()
    {
        using var handler = new LeaseStorageHandler();
        using var httpClient = new HttpClient(handler);
        var firstStore = CreateStore(httpClient);
        var secondStore = CreateStore(httpClient);
        var owner = await firstStore.AcquireLockAsync(TestContext.Current.CancellationToken);

        try
        {
            var waiting = secondStore.AcquireLockAsync(TestContext.Current.CancellationToken);

            Assert.False(waiting.IsCompleted);
            Assert.Equal(1, handler.AcquiredLeases);
            Assert.Equal("/acmebot-state/acme.example/account.lock", handler.LockPath);

            await owner.DisposeAsync();
            await using var next = await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(2, handler.AcquiredLeases);
            Assert.Equal(1, handler.CreatedBlobs);
        }
        finally
        {
            await owner.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcquireLockAsync_WhenContended_CanBeCancelled()
    {
        using var handler = new LeaseStorageHandler();
        using var httpClient = new HttpClient(handler);
        await using var owner = await CreateStore(httpClient).AcquireLockAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var waiting = CreateStore(httpClient).AcquireLockAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(1, handler.AcquiredLeases);
    }

    [Fact]
    public async Task AcquireLockAsync_WhenForbidden_PropagatesErrorWithoutRetrying()
    {
        using var handler = new LeaseStorageHandler { ForbidAcquire = true };
        using var httpClient = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<RequestFailedException>(() => CreateStore(httpClient).AcquireLockAsync(TestContext.Current.CancellationToken));

        Assert.Equal(403, error.Status);
        Assert.Equal(1, handler.AcquireAttempts);
    }

    [Fact]
    public async Task TryCreateAsync_WhenMissing_CreatesStateWithServerCondition()
    {
        using var handler = new CreateStorageHandler(HttpStatusCode.Conflict, "BlobAlreadyExists");
        using var httpClient = new HttpClient(handler);

        var created = await CreateStore(httpClient).TryCreateAsync(new { Key = "winner" }, "account_key.json", TestContext.Current.CancellationToken);

        Assert.True(created);
        Assert.Contains("winner", handler.Content);
        Assert.Equal("/acmebot-state/acme.example/account_key.json", handler.Path);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "BlobAlreadyExists")]
    [InlineData(HttpStatusCode.PreconditionFailed, "ConditionNotMet")]
    public async Task TryCreateAsync_WhenStateAlreadyExists_DoesNotReplaceIt(HttpStatusCode status, string errorCode)
    {
        using var handler = new CreateStorageHandler(status, errorCode);
        using var httpClient = new HttpClient(handler);
        var store = CreateStore(httpClient);
        Assert.True(await store.TryCreateAsync(new { Key = "winner" }, "account_key.json", TestContext.Current.CancellationToken));
        var winningState = handler.Content;

        var created = await store.TryCreateAsync(new { Key = "expired-owner" }, "account_key.json", TestContext.Current.CancellationToken);

        Assert.False(created);
        Assert.Equal(winningState, handler.Content);
    }

    [Fact]
    public async Task TryCreateAsync_WhenForbidden_DoesNotTreatErrorAsExistingState()
    {
        using var handler = new CreateStorageHandler(HttpStatusCode.Forbidden, "AuthorizationPermissionMismatch") { Content = "existing-state" };
        using var httpClient = new HttpClient(handler);

        var error = await Assert.ThrowsAsync<RequestFailedException>(() => CreateStore(httpClient).TryCreateAsync(new { Key = "new-key" }, "account_key.json", TestContext.Current.CancellationToken));

        Assert.Equal(403, error.Status);
        Assert.Equal("existing-state", handler.Content);
    }

    private sealed class CreateStorageHandler(HttpStatusCode status, string errorCode) : HttpMessageHandler
    {
        public string? Content { get; set; }
        public string? Path { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("*", request.Headers.GetValues("If-None-Match").Single());
            Path = request.RequestUri!.AbsolutePath;

            if (Content is null)
            {
                Content = await request.Content!.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent($"<Error><Code>{errorCode}</Code><Message>{errorCode}</Message></Error>")
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            response.Headers.Add("x-ms-error-code", errorCode);

            return response;
        }
    }

    private static BlobAcmeStateStore CreateStore(HttpClient httpClient)
    {
        var clientOptions = new BlobClientOptions { Transport = new HttpClientTransport(httpClient) };
        clientOptions.Retry.MaxRetries = 0;
        var container = new BlobContainerClient(new Uri("https://storage.example/acmebot-state"), clientOptions);
        var options = Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            Endpoint = new Uri("https://acme.example/directory"),
            Contacts = "admin@example.com",
            VaultBaseUrl = "https://vault.example/"
        });

        return new BlobAcmeStateStore(container, options);
    }

    private sealed class LeaseStorageHandler : HttpMessageHandler
    {
        private bool _containerExists;
        private string? _leaseId;

        public bool ForbidAcquire { get; init; }
        public int AcquireAttempts { get; private set; }
        public int AcquiredLeases { get; private set; }
        public int CreatedBlobs { get; private set; }
        public string? LockPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (this)
            {
                if (request.RequestUri!.Query.Contains("restype=container", StringComparison.Ordinal))
                {
                    if (_containerExists)
                    {
                        return Task.FromResult(Error(HttpStatusCode.Conflict, "ContainerAlreadyExists"));
                    }

                    _containerExists = true;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                }

                LockPath = request.RequestUri.AbsolutePath;

                if (!request.Headers.TryGetValues("x-ms-lease-action", out var actions))
                {
                    Assert.Contains("*", request.Headers.GetValues("If-None-Match"));

                    if (CreatedBlobs > 0)
                    {
                        return Task.FromResult(Error(HttpStatusCode.Conflict, "BlobAlreadyExists"));
                    }

                    CreatedBlobs++;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                }

                switch (actions.Single())
                {
                    case "acquire":
                        AcquireAttempts++;
                        Assert.Equal("60", request.Headers.GetValues("x-ms-lease-duration").Single());

                        if (ForbidAcquire)
                        {
                            return Task.FromResult(Error(HttpStatusCode.Forbidden, "AuthorizationPermissionMismatch"));
                        }

                        if (_leaseId is not null)
                        {
                            return Task.FromResult(Error(HttpStatusCode.Conflict, "LeaseAlreadyPresent"));
                        }

                        _leaseId = Guid.NewGuid().ToString();
                        AcquiredLeases++;
                        var acquired = new HttpResponseMessage(HttpStatusCode.Created);
                        acquired.Headers.Add("x-ms-lease-id", _leaseId);
                        return Task.FromResult(acquired);
                    case "release":
                        Assert.Equal(_leaseId, request.Headers.GetValues("x-ms-lease-id").Single());
                        _leaseId = null;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                    default:
                        throw new InvalidOperationException("Unexpected lease action.");
                }
            }
        }

        private static HttpResponseMessage Error(HttpStatusCode status, string code)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent($"<Error><Code>{code}</Code><Message>{code}</Message></Error>")
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            response.Headers.Add("x-ms-error-code", code);

            return response;
        }
    }
}
