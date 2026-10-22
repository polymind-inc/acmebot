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
        public int RenewalCount;
        public int ReleaseCount;
        public TaskCompletionSource Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<Response<BlobLease>> RenewAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref RenewalCount);
            Renewed.TrySetResult();

            return FailRenewal
                ? Task.FromException<Response<BlobLease>>(new RequestFailedException(412, "Lease lost.", "LeaseIdMismatchWithLeaseOperation", null))
                : Task.FromResult<Response<BlobLease>>(null!);
        }

        public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref ReleaseCount);

            return Task.FromResult<Response<ReleasedObjectInfo>>(null!);
        }
    }
}
