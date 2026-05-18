namespace Acmebot.App.Acme;

public interface IAcmeStateStore
{
    Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default);

    Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default);
}
