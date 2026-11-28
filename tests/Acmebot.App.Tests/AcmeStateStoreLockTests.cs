using Acmebot.App.Acme;

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
                await firstStore.SaveAsync(new { Value = "initialized" }, "account.json", firstLock.CancellationToken);
                await firstLock.DisposeAsync();

                await using var secondLock = await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                var state = await secondStore.LoadAsync<System.Text.Json.JsonElement>("account.json", secondLock.CancellationToken);

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
    public async Task FileSystemStore_RequiresKnownStateFileNames()
    {
        var directory = Directory.CreateTempSubdirectory("acmebot-state-");
        try
        {
            var store = new FileSystemAcmeStateStore(Path.Join(directory.FullName, "state"));
            var cancellationToken = TestContext.Current.CancellationToken;
            var outsidePath = Path.Join(directory.FullName, "account.json");
            await File.WriteAllTextAsync(outsidePath, "\"outside\"", cancellationToken);

            await Assert.ThrowsAsync<ArgumentException>("path", () => store.LoadAsync<string>(outsidePath, cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>("path", () => store.TryCreateAsync("new", "nested/account_key.json", cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>("path", () => store.SaveAsync("updated", "../account.json", cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>("path", () => store.SaveAsync("updated", "../STATE/account.json", cancellationToken));
            await Assert.ThrowsAsync<ArgumentException>("path", () => store.SaveAsync("updated", "account.lock", cancellationToken));
            Assert.Equal("\"outside\"", await File.ReadAllTextAsync(outsidePath, cancellationToken));

            await store.SaveAsync("inside", "account.json", cancellationToken);
            Assert.Equal("inside", await store.LoadAsync<string>("account.json", cancellationToken));
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
            await store.SaveAsync("existing", "account.json", cancellationToken);

            await Assert.ThrowsAsync<NotSupportedException>(() => store.SaveAsync(new { Type = typeof(string) }, "account.json", cancellationToken));

            Assert.Equal("existing", await store.LoadAsync<string>("account.json", cancellationToken));
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
        var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlobLock_WhenRenewalFails_CancelsProtectedOperation(bool timesOut)
    {
        var client = new TestLeaseClient
        {
            RenewalFailure = timesOut ? new OperationCanceledException("Renewal timed out.") : new RequestFailedException(412, "Lease lost.")
        };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), stateLock.CancellationToken));

        Assert.True(stateLock.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task BlobLock_WhenAzureRetriesFail_CancelsOperationAndAllowsDisposal()
    {
        var failure = new AggregateException(new RequestFailedException("Transport failed."), new OperationCanceledException("Renewal timed out."));
        var client = new TestLeaseClient { RenewalFailure = failure, ReleaseFailure = failure };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), stateLock.CancellationToken));
        await stateLock.DisposeAsync();

        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenDisposalInterruptsRetry_KeepsOperationTokenActive()
    {
        var client = new TestLeaseClient
        {
            OnRenewal = async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    throw new AggregateException(new RequestFailedException("Initial attempt failed."), ex);
                }
            }
        };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));
        var operationToken = stateLock.CancellationToken;

        await client.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await stateLock.DisposeAsync();

        Assert.False(operationToken.IsCancellationRequested);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenRetryAggregateContainsUnexpectedFailure_PropagatesFailure()
    {
        var failure = new AggregateException(new RequestFailedException("Transport failed."), new InvalidOperationException("Unexpected renewal failure."));
        var client = new TestLeaseClient { RenewalFailure = failure };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), stateLock.CancellationToken));
        var error = await Assert.ThrowsAsync<AggregateException>(() => stateLock.DisposeAsync().AsTask());

        Assert.Same(failure, error);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BlobLock_WhenTimeoutAndDisposalOverlap_UsesFirstCancellationSource(bool disposalFirst, bool wrapsCancellation)
    {
        var timeProvider = new RenewalTimeProvider();
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reportFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TestLeaseClient
        {
            OnRenewal = async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    cancellationObserved.TrySetResult();
                    await reportFailure.Task;

                    if (wrapsCancellation)
                    {
                        throw new AggregateException(new RequestFailedException("Initial attempt failed."), ex);
                    }

                    throw;
                }
            }
        };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken,
            renewalInterval: TimeSpan.FromMilliseconds(10), timeProvider: timeProvider);
        var operationToken = stateLock.CancellationToken;
        Task disposal;

        try
        {
            await client.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            if (disposalFirst)
            {
                disposal = stateLock.DisposeAsync().AsTask();
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                timeProvider.ExpireRenewal();
            }
            else
            {
                timeProvider.ExpireRenewal();
                await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                disposal = stateLock.DisposeAsync().AsTask();
            }
        }
        finally
        {
            reportFailure.TrySetResult();
        }

        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(!disposalFirst, operationToken.IsCancellationRequested);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenDisposalStartsAfterDeadlineBeforeTimerCallback_CancelsProtectedOperation()
    {
        var timeProvider = new RenewalTimeProvider();
        var client = new TestLeaseClient { OnRenewal = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var testCancellation = TestContext.Current.CancellationToken;
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, testCancellation,
            renewalInterval: TimeSpan.FromMilliseconds(10), timeProvider: timeProvider);
        var operationToken = stateLock.CancellationToken;

        await client.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
        timeProvider.AdvanceToRenewalDeadline();
        await stateLock.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), testCancellation);

        Assert.True(operationToken.IsCancellationRequested);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlobLock_WhenTimedOutRenewalCompletesLater_CancelsOperationBeforeRelease(bool callbackFails)
    {
        var timeProvider = new RenewalTimeProvider();
        var allowResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackFailure = new RequestFailedException("Unexpected failure from an operation cancellation callback.");
        var client = new TestLeaseClient { OnRenewal = _ => allowResponse.Task };
        var testCancellation = TestContext.Current.CancellationToken;
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, testCancellation,
            renewalInterval: TimeSpan.FromMilliseconds(10), timeProvider: timeProvider);
        var operationToken = stateLock.CancellationToken;
        using var registration = operationToken.Register(() =>
        {
            operationStopped.TrySetResult();
            if (callbackFails)
            {
                throw callbackFailure;
            }
        });
        Task disposal;

        try
        {
            await client.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
            timeProvider.ExpireRenewal();

            await operationStopped.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
            disposal = stateLock.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.Equal(0, client.ReleaseCount);
        }
        finally
        {
            allowResponse.TrySetResult();
        }

        if (callbackFails)
        {
            var error = await Assert.ThrowsAsync<AggregateException>(() => disposal.WaitAsync(TimeSpan.FromSeconds(5), testCancellation));
            Assert.Same(callbackFailure, Assert.Single(error.Flatten().InnerExceptions));
        }
        else
        {
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
        }

        Assert.True(operationToken.IsCancellationRequested);
        Assert.Equal(1, client.RenewalCount);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlobLock_WhenTimeoutLoggingThrows_WaitsForRenewalBeforeReleasingLease(bool cleanupFails)
    {
        var timeProvider = new RenewalTimeProvider();
        var loggerFailure = new InvalidOperationException("The logging provider failed.");
        var renewalFailure = new IOException("The renewal request failed while stopping.");
        var callbackFailure = new InvalidOperationException("The operation cancellation callback failed.");
        var logged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRenewal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new TestLeaseClient
        {
            OnRenewal = async token =>
            {
                using var registration = token.Register(() => requestCancelled.TrySetResult());
                await finishRenewal.Task;
            },
            RenewalFailure = cleanupFails ? renewalFailure : null
        };
        var testCancellation = TestContext.Current.CancellationToken;
        var logger = new CallbackLogger(() =>
        {
            logged.TrySetResult();
            throw loggerFailure;
        });
        await using var stateLock = new BlobAcmeStateStoreLock(client, logger, testCancellation,
            renewalInterval: TimeSpan.FromMilliseconds(10), timeProvider: timeProvider);
        var operationToken = stateLock.CancellationToken;
        using var operationRegistration = operationToken.Register(() =>
        {
            if (cleanupFails)
            {
                throw callbackFailure;
            }
        });
        Task disposal;

        try
        {
            await client.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
            timeProvider.ExpireRenewal();
            await logged.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
            disposal = stateLock.DisposeAsync().AsTask();

            await requestCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
            Assert.True(operationToken.IsCancellationRequested);
            Assert.False(disposal.IsCompleted);
            Assert.Equal(0, client.ReleaseCount);
        }
        finally
        {
            finishRenewal.TrySetResult();
        }

        if (cleanupFails)
        {
            var error = await Assert.ThrowsAsync<AggregateException>(() => disposal.WaitAsync(TimeSpan.FromSeconds(5), testCancellation));
            var failures = error.Flatten().InnerExceptions;
            Assert.Equal(3, failures.Count);
            Assert.Contains(loggerFailure, failures);
            Assert.Contains(renewalFailure, failures);
            Assert.Contains(callbackFailure, failures);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => disposal.WaitAsync(TimeSpan.FromSeconds(5), testCancellation));
            Assert.Same(loggerFailure, error);
        }

        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenRenewalFails_CancelsProtectedOperationBeforeLogging()
    {
        var loggingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishLogging = new ManualResetEventSlim();
        var testCancellation = TestContext.Current.CancellationToken;
        var logger = new CallbackLogger(() =>
        {
            loggingStarted.TrySetResult();
            if (!finishLogging.Wait(TimeSpan.FromSeconds(5), testCancellation))
            {
                throw new TimeoutException("The test did not release the logger.");
            }
        });
        var client = new TestLeaseClient { RenewalFailure = new RequestFailedException(412, "Lease lost.") };
        await using var stateLock = new BlobAcmeStateStoreLock(client, logger, testCancellation, TimeSpan.FromMilliseconds(10));

        try
        {
            await loggingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), testCancellation);
            Assert.True(stateLock.CancellationToken.IsCancellationRequested);
        }
        finally
        {
            finishLogging.Set();
        }
    }

    [Fact]
    public async Task BlobLock_WhenRenewalFailsUnexpectedly_CancelsOperationAndReleasesLease()
    {
        var failure = new InvalidOperationException("Unexpected renewal failure.");
        var client = new TestLeaseClient { RenewalFailure = failure };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(TimeSpan.FromSeconds(5), stateLock.CancellationToken));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => stateLock.DisposeAsync().AsTask());

        Assert.Same(failure, error);
        Assert.Equal(1, client.ReleaseCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BlobLock_WhenCancellationCallbackThrows_PreservesFailuresAndAttemptsLeaseRelease(bool unexpectedRenewalFailure, bool releaseFails)
    {
        var allowRenewal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewalFailure = unexpectedRenewalFailure
            ? (Exception)new IOException("Unexpected renewal failure.")
            : new RequestFailedException(412, "Lease lost.");
        var callbackFailure = new InvalidOperationException("Simulated cancellation callback failure.");
        var releaseFailure = new IOException("Unexpected lease release failure.");
        var client = new TestLeaseClient
        {
            RenewalFailure = renewalFailure,
            RenewalGate = allowRenewal.Task,
            ReleaseFailure = releaseFails ? releaseFailure : null
        };
        await using var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, TestContext.Current.CancellationToken, TimeSpan.FromMilliseconds(10));
        var callbackInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stateLock.CancellationToken.Register(() =>
        {
            callbackInvoked.TrySetResult();
            throw callbackFailure;
        });
        allowRenewal.SetResult();

        await callbackInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<AggregateException>(() => stateLock.DisposeAsync().AsTask());

        var failures = error.Flatten().InnerExceptions;
        var expectedFailures = new List<Exception> { callbackFailure };
        if (unexpectedRenewalFailure)
        {
            expectedFailures.Add(renewalFailure);
        }

        if (releaseFails)
        {
            expectedFailures.Add(releaseFailure);
        }

        Assert.Equal(expectedFailures.Count, failures.Count);
        Assert.All(expectedFailures, failure => Assert.Contains(failure, failures));
        Assert.Equal(1, client.ReleaseCount);
    }

    [Fact]
    public async Task BlobLock_WhenCallerCancels_StillReleasesWithIndependentToken()
    {
        var client = new TestLeaseClient();
        using var cancellation = new CancellationTokenSource();
        var stateLock = new BlobAcmeStateStoreLock(client, NullLogger.Instance, cancellation.Token);

        await cancellation.CancelAsync();
        Assert.True(stateLock.CancellationToken.IsCancellationRequested);
        await stateLock.DisposeAsync();

        Assert.Equal(1, client.ReleaseCount);
    }

    private sealed class CallbackLogger(Action onLog) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => onLog();
    }

    private sealed class RenewalTimeProvider : TimeProvider
    {
        private RenewalTimer? _timer;
        private long _timestamp;
        private long _deadline;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Exchange(ref _deadline, GetTimestamp() + dueTime.Ticks);
            return _timer = new RenewalTimer(callback, state);
        }

        public void AdvanceToRenewalDeadline() => Interlocked.Exchange(ref _timestamp, Interlocked.Read(ref _deadline));

        public void ExpireRenewal()
        {
            AdvanceToRenewalDeadline();
            Assert.IsType<RenewalTimer>(_timer).Fire();
        }

        private sealed class RenewalTimer(TimerCallback callback, object? state) : ITimer
        {
            private TimerCallback? _callback = callback;

            public void Fire()
            {
                var expire = Interlocked.Exchange(ref _callback, null);
                Assert.NotNull(expire);
                expire(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

            public void Dispose() => Interlocked.Exchange(ref _callback, null);

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class TestLeaseClient : BlobLeaseClient
    {
        public Exception? RenewalFailure { get; init; }
        public Exception? ReleaseFailure { get; init; }
        public Func<CancellationToken, Task>? OnRenewal { get; init; }
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

            if (OnRenewal is not null)
            {
                await OnRenewal(cancellationToken);
            }

            return RenewalFailure is { } failure ? throw failure : null!;
        }

        public override Task<Response<ReleasedObjectInfo>> ReleaseAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref ReleaseCount);

            return ReleaseFailure is { } failure
                ? Task.FromException<Response<ReleasedObjectInfo>>(failure)
                : Task.FromResult<Response<ReleasedObjectInfo>>(null!);
        }
    }
}
