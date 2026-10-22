namespace Acmebot.App.Acme;

public interface IAcmeStateStore
{
    // Hold this lock across the entire read/create/save sequence, not individual state operations.
    Task<IAcmeStateStoreLock> AcquireLockAsync(CancellationToken cancellationToken = default);

    Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default);

    // Atomically create state without replacing an existing value, even if lock ownership is lost.
    Task<bool> TryCreateAsync<TState>(TState value, string path, CancellationToken cancellationToken = default);

    Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default);
}

public interface IAcmeStateStoreLock : IAsyncDisposable
{
    // Cancelled when the caller cancels or the distributed lock can no longer be maintained.
    CancellationToken CancellationToken { get; }
}
