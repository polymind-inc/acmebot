using Acmebot.App.Acme;

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class AcmeStateStoreLockTests
{
    [Fact]
    public async Task FileSystemLock_SerializesIndependentStoresAndPreservesState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"acmebot-state-{Guid.NewGuid():N}");

        try
        {
            var firstStore = new FileSystemAcmeStateStore(directory);
            var secondStore = new FileSystemAcmeStateStore(directory);
            var firstLock = await firstStore.AcquireLockAsync(TestContext.Current.CancellationToken);

            try
            {
                var waiting = secondStore.AcquireLockAsync(TestContext.Current.CancellationToken);

                Assert.False(waiting.IsCompleted);
                await firstStore.SaveAsync(new { Value = "initialized" }, "state.json", firstLock.CancellationToken);
                await firstLock.DisposeAsync();

                await using var secondLock = await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                var state = await secondStore.LoadAsync<System.Text.Json.JsonElement>("state.json", secondLock.CancellationToken);

                Assert.Equal("initialized", state.GetProperty("Value").GetString());
            }
            finally
            {
                await firstLock.DisposeAsync();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileSystemLock_WaitCanBeCancelledWithoutReleasingOwner()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"acmebot-state-{Guid.NewGuid():N}");

        try
        {
            var store = new FileSystemAcmeStateStore(directory);
            await using var owner = await store.AcquireLockAsync(TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            var waiting = new FileSystemAcmeStateStore(directory).AcquireLockAsync(cancellation.Token);

            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.False(owner.CancellationToken.IsCancellationRequested);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileSystemStore_RequiresPathsInsideStateDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("acmebot-state-");
        try
        {
            var store = new FileSystemAcmeStateStore(Path.Join(directory.FullName, "state"));
            var cancellationToken = TestContext.Current.CancellationToken;
            var outsidePath = Path.Join(directory.FullName, "outside.json");
            await File.WriteAllTextAsync(outsidePath, "\"outside\"", cancellationToken);

            await Assert.ThrowsAsync<ArgumentException>("path", () => store.LoadAsync<string>(outsidePath, cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>("path", () => store.TryCreateAsync("new", "../new.json", cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>("path", () => store.SaveAsync("updated", "../outside.json", cancellationToken));
            Assert.Equal("\"outside\"", await File.ReadAllTextAsync(outsidePath, cancellationToken));
            Assert.False(File.Exists(Path.Join(directory.FullName, "new.json")));

            await store.SaveAsync("inside", "nested/state.json", cancellationToken);
            Assert.Equal("inside", await store.LoadAsync<string>("nested/state.json", cancellationToken));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task FileSystemStore_WhenSerializationFails_RemovesTemporaryFileAndPreservesState()
    {
        var directory = Directory.CreateTempSubdirectory("acmebot-state-");
        try
        {
            var store = new FileSystemAcmeStateStore(directory.FullName);
            var cancellationToken = TestContext.Current.CancellationToken;
            await store.SaveAsync("existing", "state.json", cancellationToken);

            await Assert.ThrowsAsync<NotSupportedException>(() => store.SaveAsync(new { Type = typeof(string) }, "state.json", cancellationToken));

            Assert.Equal("existing", await store.LoadAsync<string>("state.json", cancellationToken));
            Assert.Empty(directory.EnumerateFiles("*.tmp"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task BlobLock_RenewsUntilDisposedAndReleasesOnlyOnce()
    {
        var client = new TestLeaseClient();
        var stateLock = new BlobAcmeStateStoreLock(client, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

        try
        {
            await client.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(stateLock.CancellationToken.IsCancellationRequested);
        }
        finally
        {
            await stateLock.DisposeAsync();
        }

        var renewals = client.RenewalCount;
        await stateLock.DisposeAsync();

        Assert.True(renewals > 0);
        Assert.Equal(renewals, client.RenewalCount);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenRenewalFails_CancelsProtectedOperation()
    {
        var client = new TestLeaseClient { FailRenewal = true };
        await using var stateLock = new BlobAcmeStateStoreLock(client, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), stateLock.CancellationToken));

        Assert.True(stateLock.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task BlobLock_WhenCancellationCallbackThrows_StillReleasesLease()
    {
        var allowRenewal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TestLeaseClient { FailRenewal = true, RenewalGate = allowRenewal.Task };
        await using var stateLock = new BlobAcmeStateStoreLock(client, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));
        var callbackInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stateLock.CancellationToken.Register(() =>
        {
            callbackInvoked.TrySetResult();
            throw new InvalidOperationException("Simulated cancellation callback failure.");
        });
        allowRenewal.SetResult();

        await callbackInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<AggregateException>(() => stateLock.DisposeAsync().AsTask());

        Assert.IsType<InvalidOperationException>(Assert.Single(error.InnerExceptions));
        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenCallerCancels_StillReleasesWithIndependentToken()
    {
        var client = new TestLeaseClient();
        using var cancellation = new CancellationTokenSource();
        var stateLock = new BlobAcmeStateStoreLock(client, cancellation.Token);

        await cancellation.CancelAsync();
        Assert.True(stateLock.CancellationToken.IsCancellationRequested);
        await stateLock.DisposeAsync();

        Assert.Equal(1, client.ReleaseCount);
    }

    private sealed class TestLeaseClient : BlobLeaseClient
    {
        public bool FailRenewal { get; init; }
        public Task RenewalGate { get; init; } = Task.CompletedTask;
        public int RenewalCount;
        public int ReleaseCount;
        public TaskCompletionSource Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<Response<BlobLease>> RenewAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
        {
            await RenewalGate.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref RenewalCount);
            Renewed.TrySetResult();

            return FailRenewal
                ? throw new RequestFailedException(412, "Lease lost.", "LeaseIdMismatchWithLeaseOperation", null)
                : null!;
        }

        public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref ReleaseCount);

            return Task.FromResult<Response<ReleasedObjectInfo>>(null!);
        }
    }
}
