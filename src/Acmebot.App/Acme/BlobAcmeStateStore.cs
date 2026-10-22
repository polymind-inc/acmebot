using System.Text.Json;

using Acmebot.App.Options;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

internal sealed class BlobAcmeStateStore(BlobContainerClient containerClient, IOptions<AcmebotOptions> options) : IAcmeStateStore
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

                return new BlobAcmeStateStoreLock(leaseClient, cancellationToken);
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

internal sealed class BlobAcmeStateStoreLock : IAcmeStateStoreLock
{
    private readonly BlobLeaseClient _leaseClient;
    private readonly CancellationTokenSource _operationCancellation;
    private readonly CancellationTokenSource _renewalCancellation = new();
    private readonly Task _renewalTask;
    private int _disposed;

    public BlobAcmeStateStoreLock(BlobLeaseClient leaseClient, CancellationToken cancellationToken, TimeSpan? renewalInterval = null)
    {
        _leaseClient = leaseClient;
        _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _renewalTask = RenewAsync(renewalInterval ?? TimeSpan.FromSeconds(20));
    }

    public CancellationToken CancellationToken => _operationCancellation.Token;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _renewalCancellation.CancelAsync();
        await _renewalTask;

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
        finally
        {
            _operationCancellation.Dispose();
            _renewalCancellation.Dispose();
        }
    }

    private async Task RenewAsync(TimeSpan renewalInterval)
    {
        try
        {
            using var timer = new PeriodicTimer(renewalInterval);

            while (await timer.WaitForNextTickAsync(_renewalCancellation.Token))
            {
                using var renewalTimeout = CancellationTokenSource.CreateLinkedTokenSource(_renewalCancellation.Token);
                renewalTimeout.CancelAfter(TimeSpan.FromSeconds(20));

                await _leaseClient.RenewAsync(cancellationToken: renewalTimeout.Token);
            }
        }
        catch (OperationCanceledException) when (_renewalCancellation.IsCancellationRequested)
        {
            // Normal disposal stops the renewal loop.
        }
        catch (Exception)
        {
            // Fail closed: callers must stop using state when lease ownership is uncertain.
            await _operationCancellation.CancelAsync();
        }
    }
}
