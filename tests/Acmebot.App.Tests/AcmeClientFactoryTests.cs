using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Acmebot.Acme;
using Acmebot.App.Acme;
using Acmebot.App.Options;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AcmeClientFactoryTests
{
    [Fact]
    public async Task CreateClientAsync_ConcurrentFactories_RegisterOneAccountAndReuseKey()
    {
        using var state = new TestStateDirectory();
        using var handler = new AccountHandler { PauseRegistration = true };
        using var httpClient = new HttpClient(handler);
        using var firstFactory = CreateFactory(new FileSystemAcmeStateStore(state.Path), httpClient);
        using var secondFactory = CreateFactory(new FileSystemAcmeStateStore(state.Path), httpClient);
        var first = firstFactory.CreateClientAsync(TestContext.Current.CancellationToken);

        try
        {
            await handler.Registering.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(File.Exists(System.IO.Path.Combine(state.Path, "account_key.json")));

            var second = secondFactory.CreateClientAsync(TestContext.Current.CancellationToken);

            Assert.False(second.IsCompleted);
            handler.Proceed.TrySetResult();

            var contexts = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal(1, handler.RegistrationCount);
            Assert.Equal(contexts[0].Account.AccountUrl, contexts[1].Account.AccountUrl);
            Assert.Equal(contexts[0].Signer.GetThumbprint(), contexts[1].Signer.GetThumbprint());
            Assert.Same(contexts[0], await firstFactory.CreateClientAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            handler.Proceed.TrySetResult();
            await first;
        }
    }

    [Fact]
    public async Task CreateClientAsync_WithCompleteState_DoesNotAcquireInitializationLock()
    {
        using var state = new TestStateDirectory();
        var store = new FileSystemAcmeStateStore(state.Path);
        using var handler = new AccountHandler();
        using var httpClient = new HttpClient(handler);
        using var firstFactory = CreateFactory(store, httpClient);
        var first = await firstFactory.CreateClientAsync(TestContext.Current.CancellationToken);
        using var secondFactory = CreateFactory(new LockRejectingStore(store), httpClient);

        var second = await secondFactory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.RegistrationCount);
        Assert.Equal(first.Signer.GetThumbprint(), second.Signer.GetThumbprint());
    }

    [Fact]
    public async Task CreateClientAsync_WhenSavingAccountFails_RetryUsesPersistedKey()
    {
        using var state = new TestStateDirectory();
        var store = new FailingAccountStore(new FileSystemAcmeStateStore(state.Path));
        using var handler = new AccountHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(store, httpClient);

        await Assert.ThrowsAsync<IOException>(() => factory.CreateClientAsync(TestContext.Current.CancellationToken));

        Assert.True(File.Exists(System.IO.Path.Combine(state.Path, "account_key.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(state.Path, "account.json")));

        var context = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.RegistrationCount);
        Assert.Equal(handler.RegistrationKeys[0], handler.RegistrationKeys[1]);
        Assert.Equal(new Uri("https://acme.example/accounts/1"), context.Account.AccountUrl);
    }

    [Fact]
    public async Task CreateClientAsync_WhenWaitingIsCancelled_LeavesOwnerStateUsable()
    {
        using var state = new TestStateDirectory();
        var store = new FileSystemAcmeStateStore(state.Path);
        var owner = await store.AcquireLockAsync(TestContext.Current.CancellationToken);
        using var handler = new AccountHandler();
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(new FileSystemAcmeStateStore(state.Path), httpClient);
        using var cancellation = new CancellationTokenSource();

        try
        {
            var waiting = factory.CreateClientAsync(cancellation.Token);
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.Equal(0, handler.RegistrationCount);
        }
        finally
        {
            await owner.DisposeAsync();
        }

        await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.RegistrationCount);
    }

    [Fact]
    public async Task CreateClientAsync_WhenLockIsLost_CancelsRegistrationAndDoesNotCacheClient()
    {
        using var state = new TestStateDirectory();
        using var lostLock = new CancellationTokenSource();
        var store = new CancellingStore(new FileSystemAcmeStateStore(state.Path), lostLock.Token);
        using var handler = new AccountHandler { PauseRegistration = true };
        using var httpClient = new HttpClient(handler);
        using var factory = CreateFactory(store, httpClient);
        var initializing = factory.CreateClientAsync(TestContext.Current.CancellationToken);

        await handler.Registering.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await lostLock.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initializing);
        Assert.False(File.Exists(System.IO.Path.Combine(state.Path, "account.json")));

        // The failed initializer released its lock and retained the key for a different worker.
        handler.Proceed.TrySetResult();
        using var retryFactory = CreateFactory(new FileSystemAcmeStateStore(state.Path), httpClient);
        await retryFactory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Equal(handler.RegistrationKeys[0], handler.RegistrationKeys[1]);
    }

    [Fact]
    public async Task CreateClientAsync_WhenExpiredOwnerResumes_DoesNotOverwriteWinningKey()
    {
        using var state = new TestStateDirectory();
        var store = new FileSystemAcmeStateStore(state.Path);
        var pausedStore = new PausedKeyStore(store);
        using var handler = new AccountHandler();
        using var httpClient = new HttpClient(handler);
        using var expiredFactory = CreateFactory(pausedStore, httpClient);
        using var winningFactory = CreateFactory(store, httpClient);
        var initializing = expiredFactory.CreateClientAsync(TestContext.Current.CancellationToken);

        try
        {
            await pausedStore.KeyWritePending.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Model expiration before the old worker's renewal loop detects the lost lock.
            var expiredLock = Assert.IsAssignableFrom<IAcmeStateStoreLock>(pausedStore.StateLock);
            await expiredLock.DisposeAsync();
            Assert.False(expiredLock.CancellationToken.IsCancellationRequested);

            var winner = await winningFactory.CreateClientAsync(TestContext.Current.CancellationToken);
            var keyPath = System.IO.Path.Combine(state.Path, "account_key.json");
            var accountPath = System.IO.Path.Combine(state.Path, "account.json");
            var winningKey = await File.ReadAllBytesAsync(keyPath, TestContext.Current.CancellationToken);
            var winningAccount = await File.ReadAllBytesAsync(accountPath, TestContext.Current.CancellationToken);

            pausedStore.Resume.TrySetResult();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => initializing);

            Assert.Contains("created by another worker", error.Message);
            Assert.Equal(winningKey, await File.ReadAllBytesAsync(keyPath, TestContext.Current.CancellationToken));
            Assert.Equal(winningAccount, await File.ReadAllBytesAsync(accountPath, TestContext.Current.CancellationToken));
            Assert.Equal(1, handler.RegistrationCount);

            var retry = await expiredFactory.CreateClientAsync(TestContext.Current.CancellationToken);

            Assert.Equal(winner.Signer.GetThumbprint(), retry.Signer.GetThumbprint());
            Assert.Equal(winner.Account.AccountUrl, retry.Account.AccountUrl);
            Assert.Equal(1, handler.RegistrationCount);
        }
        finally
        {
            pausedStore.Resume.TrySetResult();

            try
            {
                await initializing;
            }
            catch (InvalidOperationException)
            {
                // The expired initializer is expected to abort when the winning key already exists.
            }
        }
    }

    private static AcmeClientFactory CreateFactory(IAcmeStateStore store, HttpClient httpClient)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AcmebotOptions
        {
            Endpoint = new Uri("https://acme.example/directory"),
            Contacts = "admin@example.com",
            VaultBaseUrl = "https://vault.example/"
        });

        return new AcmeClientFactory(options, store, () => new AcmeClient(httpClient, options.Value.Endpoint));
    }

    private sealed class TestStateDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"acmebot-account-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FailingAccountStore(IAcmeStateStore inner) : IAcmeStateStore
    {
        private bool _failSave = true;

        public Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default) => inner.AcquireLockAsync(cancellationToken);

        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default) => inner.LoadAsync<TState>(path, cancellationToken);

        public Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => inner.TryCreateAsync(value, path, cancellationToken);

        public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
        {
            if (path == "account.json" && _failSave)
            {
                _failSave = false;
                throw new IOException("Simulated failure after ACME registration.");
            }

            return inner.SaveAsync(value, path, cancellationToken);
        }
    }

    private sealed class LockRejectingStore(IAcmeStateStore inner) : IAcmeStateStore
    {
        public Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Complete account state must not acquire a lock.");

        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default) => inner.LoadAsync<TState>(path, cancellationToken);

        public Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => inner.TryCreateAsync(value, path, cancellationToken);

        public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => inner.SaveAsync(value, path, cancellationToken);
    }

    private sealed class CancellingStore(IAcmeStateStore inner, CancellationToken lockLost) : IAcmeStateStore
    {
        public async Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default)
        {
            var stateLock = await inner.AcquireLockAsync(cancellationToken);

            return new CancellingLock(stateLock, lockLost);
        }

        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default) => inner.LoadAsync<TState>(path, cancellationToken);

        public Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => inner.TryCreateAsync(value, path, cancellationToken);

        public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => inner.SaveAsync(value, path, cancellationToken);

        private sealed class CancellingLock(IAcmeStateStoreLock inner, CancellationToken lockLost) : IAcmeStateStoreLock
        {
            private readonly CancellationTokenSource _cancellation = CancellationTokenSource.CreateLinkedTokenSource(inner.CancellationToken, lockLost);

            public CancellationToken CancellationToken => _cancellation.Token;

            public async ValueTask DisposeAsync()
            {
                await inner.DisposeAsync();
                _cancellation.Dispose();
            }
        }
    }

    private sealed class PausedKeyStore(IAcmeStateStore inner) : IAcmeStateStore
    {
        public IAcmeStateStoreLock? StateLock { get; private set; }
        public TaskCompletionSource KeyWritePending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default)
        {
            StateLock = await inner.AcquireLockAsync(cancellationToken);

            return StateLock;
        }

        public Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default) => inner.LoadAsync<TState>(path, cancellationToken);

        public async Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
        {
            await WaitBeforeKeyWriteAsync(path, cancellationToken);

            return await inner.TryCreateAsync(value, path, cancellationToken);
        }

        public async Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
        {
            await WaitBeforeKeyWriteAsync(path, cancellationToken);
            await inner.SaveAsync(value, path, cancellationToken);
        }

        private async Task WaitBeforeKeyWriteAsync(string path, CancellationToken cancellationToken)
        {
            if (path == "account_key.json")
            {
                KeyWritePending.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class AccountHandler : HttpMessageHandler
    {
        public bool PauseRegistration { get; init; }
        public int RegistrationCount;
        public List<string> RegistrationKeys { get; } = [];
        public TaskCompletionSource Registering { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Proceed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                        newAccount = "https://acme.example/register",
                        newOrder = "https://acme.example/order"
                    });
                    break;
                case "/register":
                    var message = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
                    using (var header = JsonDocument.Parse(Base64Url.DecodeFromChars(message.GetProperty("protected").GetString()!)))
                    {
                        RegistrationKeys.Add(header.RootElement.GetProperty("jwk").GetRawText());
                    }

                    Interlocked.Increment(ref RegistrationCount);
                    Registering.TrySetResult();

                    if (PauseRegistration)
                    {
                        await Proceed.Task.WaitAsync(cancellationToken);
                    }

                    response.StatusCode = HttpStatusCode.Created;
                    response.Headers.Location = new Uri("https://acme.example/accounts/1");
                    response.Content = JsonContent.Create(new { status = "valid" });
                    break;
                case "/nonce":
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            }

            return response;
        }
    }
}
