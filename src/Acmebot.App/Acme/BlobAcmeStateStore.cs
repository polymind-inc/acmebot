using System.Text.Json;

using Acmebot.App.Options;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

internal sealed class BlobAcmeStateStore(BlobContainerClient containerClient, IOptions<AcmebotOptions> options, ILogger<BlobAcmeStateStore> logger) : IAcmeStateStore
{
    private const string ContentType = "application/json";

    private readonly Uri _endpoint = options.Value.Endpoint;

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default)
    {
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var lockBlob = containerClient.GetBlobClient(CreateBlobName("account.lock"));

        try
        {
            await lockBlob.UploadAsync(BinaryData.FromString(string.Empty), overwrite: false, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists ||
                                                ex.ErrorCode == BlobErrorCode.LeaseIdMissing)
        {
            // Another worker already created the lock blob. Never overwrite or delete it.
        }

        var leaseClient = lockBlob.GetBlobLeaseClient();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await leaseClient.AcquireAsync(TimeSpan.FromSeconds(60), cancellationToken: cancellationToken);

                return new BlobAcmeStateStoreLock(leaseClient, logger, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.LeaseAlreadyPresent ||
                                                    ex.ErrorCode == BlobErrorCode.LeaseIsBreakingAndCannotBeAcquired)
            {
                await Task.Delay(Random.Shared.Next(250, 750), cancellationToken);
            }
        }
    }

    public async Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = containerClient.GetBlobClient(CreateBlobName(path));
            var response = await blobClient.DownloadContentAsync(cancellationToken);

            return response.Value.Content.ToObjectFromJson<TState>(s_jsonSerializerOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            if (ex.ErrorCode == BlobErrorCode.ContainerNotFound)
            {
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            return default;
        }
    }

    public async Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveCoreAsync(value, path, overwrite: false, cancellationToken);

            return true;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobAlreadyExists ||
                                                ex.ErrorCode == BlobErrorCode.ConditionNotMet)
        {
            return false;
        }
    }

    public Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default) => SaveCoreAsync(value, path, overwrite: true, cancellationToken);

    private async Task SaveCoreAsync<TState>(TState value, string path, bool overwrite, CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(CreateBlobName(path));
        var content = BinaryData.FromObjectAsJson(value, s_jsonSerializerOptions);

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                Conditions = overwrite ? null : new BlobRequestConditions { IfNoneMatch = ETag.All },
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentType
                }
            },
            cancellationToken);
    }

    private string CreateBlobName(string path) => $"{_endpoint.Host}/{path}";
}

internal sealed partial class BlobAcmeStateStoreLock : IAcmeStateStoreLock
{
    private readonly BlobLeaseClient _leaseClient;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _operationCancellation;
    private readonly CancellationTokenSource _renewalCancellation = new();
    private readonly Task _renewalTask;
    private readonly Lock _renewalGate = new();
    private TaskCompletionSource<bool>? _activeRenewal;
    private bool _disposed;

    public BlobAcmeStateStoreLock(BlobLeaseClient leaseClient, ILogger logger, CancellationToken cancellationToken, TimeSpan? renewalInterval = null, TimeProvider? timeProvider = null)
    {
        _leaseClient = leaseClient;
        _logger = logger;
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _renewalTask = RenewAsync(renewalInterval ?? TimeSpan.FromSeconds(20), timeProvider ?? TimeProvider.System);
    }

    public CancellationToken CancellationToken => _operationCancellation.Token;

    public async ValueTask DisposeAsync()
    {
        lock (_renewalGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _activeRenewal?.TrySetResult(true);
        }

        using var operationCancellation = _operationCancellation;
        using var renewalCancellation = _renewalCancellation;
        Exception? renewalFailure = null;

        try
        {
            await AwaitBothAsync(renewalCancellation.CancelAsync(), _renewalTask);
        }
        catch (Exception ex)
        {
            renewalFailure = ex;
            throw;
        }
        finally
        {
            try
            {
                await ReleaseLeaseAsync();
            }
            catch (Exception releaseFailure) when (renewalFailure is not null)
            {
                throw new AggregateException(renewalFailure, releaseFailure);
            }
        }
    }

    private async Task ReleaseLeaseAsync()
    {
        try
        {
            // Cleanup must still run when the caller or lease-loss token has been cancelled.
            using var releaseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _leaseClient.ReleaseAsync(cancellationToken: releaseTimeout.Token);
        }
        catch (Exception ex) when (ex is RequestFailedException or OperationCanceledException)
        {
            // The finite lease expires after a worker crash or an unsuccessful release.
        }
        catch (AggregateException ex) when (IsExpectedRetryFailure(ex))
        {
            // The Azure retry policy can aggregate failures from multiple attempts.
        }
    }

    private async Task RenewAsync(TimeSpan renewalInterval, TimeProvider timeProvider)
    {
        var disposalToken = _renewalCancellation.Token;

        try
        {
            using var timer = new PeriodicTimer(renewalInterval);

            while (await timer.WaitForNextTickAsync(disposalToken))
            {
                if (!await RenewLeaseAsync(timeProvider))
                {
                    return;
                }
            }

            return;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == disposalToken && disposalToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is RequestFailedException or OperationCanceledException ||
                                   ex is AggregateException retryFailure && IsExpectedRetryFailure(retryFailure))
        {
            LogLeaseRenewalFailed(_logger, ex);
        }
        catch (Exception renewalFailure)
        {
            try
            {
                await _operationCancellation.CancelAsync();
            }
            catch (AggregateException cancellationFailure)
            {
                throw new AggregateException(renewalFailure, cancellationFailure);
            }

            throw;
        }

        await _operationCancellation.CancelAsync();
    }

    private async Task<bool> RenewLeaseAsync(TimeProvider timeProvider)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20), timeProvider);
        using var requestCancellation = new CancellationTokenSource();
        // Only the first source can complete the signal. Record it before cancelling the request.
        var disposalWon = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeoutRegistration = timeout.Token.Register(() =>
        {
            lock (_renewalGate)
            {
                disposalWon.TrySetResult(false);
            }
        });
        lock (_renewalGate)
        {
            if (_disposed)
            {
                return false;
            }

            _activeRenewal = disposalWon;
        }

        var requestToken = requestCancellation.Token;
        var cancelledByDisposal = false;

        try
        {
            var renewal = _leaseClient.RenewAsync(cancellationToken: requestToken);
            if (await Task.WhenAny(renewal, disposalWon.Task) == disposalWon.Task)
            {
                cancelledByDisposal = await disposalWon.Task;
                await AwaitBothAsync(requestCancellation.CancelAsync(), renewal);
            }
            else
            {
                await renewal;
            }

            return true;
        }
        catch (Exception ex) when (cancelledByDisposal && IsRequestCancellation(ex, requestToken))
        {
            return false;
        }
        finally
        {
            lock (_renewalGate)
            {
                _activeRenewal = null;
            }
        }
    }

    private static bool IsRequestCancellation(Exception exception, CancellationToken requestToken)
    {
        if (exception is AggregateException retryFailure && IsExpectedRetryFailure(retryFailure))
        {
            exception = retryFailure.InnerExceptions[^1];
        }

        return exception is OperationCanceledException cancelled && cancelled.CancellationToken == requestToken;
    }

    private static async Task AwaitBothAsync(Task first, Task second)
    {
        var completion = Task.WhenAll(first, second);
        try
        {
            await completion;
        }
        catch (Exception) when (completion.Exception is { InnerExceptions.Count: > 1 } failures)
        {
            throw failures;
        }
    }

    private static bool IsExpectedRetryFailure(AggregateException exception)
        => exception.InnerExceptions.Count > 0 && exception.InnerExceptions.All(failure => failure is RequestFailedException or OperationCanceledException);

    [LoggerMessage(LogLevel.Warning, "Failed to renew the ACME state lock blob lease. Cancelling the state operation.")]
    private static partial void LogLeaseRenewalFailed(ILogger logger, Exception exception);
}
