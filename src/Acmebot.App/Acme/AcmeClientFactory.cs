using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Infrastructure;
using Acmebot.App.Options;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

public sealed class AcmeClientFactory(IOptions<AcmebotOptions> options, IAcmeStateStore stateStore) : IDisposable
{
    private readonly AcmebotOptions _options = options.Value;
    private readonly Func<AcmeClient> _createClient = () => new AcmeClient(
        options.Value.Endpoint,
        new AcmeClientOptions { UserAgent = $"Acmebot/{Constants.ApplicationVersion}" });
    private readonly SemaphoreSlim _clientContextLock = new(1, 1);
    private AcmeClientContext? _clientContext;
    private bool _disposed;

    internal AcmeClientFactory(IOptions<AcmebotOptions> options, IAcmeStateStore stateStore, Func<AcmeClient> createClient)
        : this(options, stateStore) => _createClient = createClient;

    public async Task<AcmeClientContext> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _clientContextLock.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _clientContext ??= await CreateClientCoreAsync(cancellationToken);
        }
        finally
        {
            _clientContextLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clientContextLock.Dispose();
        _clientContext?.Dispose();
    }

    private async Task<AcmeClientContext> CreateClientCoreAsync(CancellationToken cancellationToken)
    {
        var account = await stateStore.LoadAsync<AccountDetails>("account.json", cancellationToken);
        var accountKey = await stateStore.LoadAsync<AccountKey>("account_key.json", cancellationToken);

        // Existing state is immutable during initialization, so established workers need no lock.
        await using var stateLock = account is null || accountKey is null
            ? await stateStore.AcquireLockAsync(cancellationToken)
            : null;
        var stateCancellationToken = stateLock?.CancellationToken ?? cancellationToken;

        if (stateLock is not null)
        {
            // Another worker may have initialized state while this worker was waiting.
            account = await stateStore.LoadAsync<AccountDetails>("account.json", stateCancellationToken);
            accountKey = await stateStore.LoadAsync<AccountKey>("account_key.json", stateCancellationToken);
        }
        var contacts = GetContacts();
        var isNewAccountKey = false;

        if (accountKey is null)
        {
            if (account is not null)
            {
                throw new PreconditionException("The ACME account exists, but its private key could not be found.");
            }

            accountKey = AccountKey.CreateDefault();
            isNewAccountKey = true;
        }

        var signer = accountKey.GenerateSigner();
        AcmeClient? client = null;

        // The client owns an HttpClient, and both it and the signer are only handed to the caller once
        // the context has been fully built. Anything that fails in between (a directory fetch against an
        // unreachable ACME endpoint, missing EAB credentials, a state store write) would otherwise leak
        // them on every retry.
        try
        {
            client = _createClient();

            var directory = await client.GetDirectoryAsync(stateCancellationToken);
            AcmeAccountHandle accountHandle;

            if (account is null)
            {
                var externalAccountBinding = CreateExternalAccountBinding();

                if (externalAccountBinding is null && (directory.Metadata?.ExternalAccountRequired ?? false))
                {
                    throw new PreconditionException("This ACME endpoint requires External Account Binding (EAB). Configure EAB credentials and try again.");
                }

                if (isNewAccountKey)
                {
                    // Creation must be atomic even if this worker resumes after its lease has expired.
                    // Never register with a key that lost a race to another initializer.
                    if (!await stateStore.TryCreateAsync(accountKey, "account_key.json", stateCancellationToken))
                    {
                        throw new InvalidOperationException("The ACME account key was created by another worker. Retry initialization to use the existing account key.");
                    }
                }

                accountHandle = await client.CreateAccountAsync(
                    signer,
                    new AcmeNewAccountRequest
                    {
                        Contact = contacts,
                        TermsOfServiceAgreed = true
                    },
                    externalAccountBinding,
                    stateCancellationToken);
                account = AccountDetails.FromAccountHandle(accountHandle, directory.Metadata?.TermsOfService);

                await stateStore.SaveAsync(account, "account.json", stateCancellationToken);
            }
            else
            {
                accountHandle = account.ToAccountHandle(signer);
            }

            stateCancellationToken.ThrowIfCancellationRequested();

            return new AcmeClientContext
            {
                Client = client,
                Directory = directory,
                Signer = signer,
                Account = accountHandle
            };
        }
        catch
        {
            // Same order as AcmeClientContext.Dispose(), which owns these once the context exists.
            signer.Dispose();
            client?.Dispose();

            throw;
        }
    }

    private AcmeExternalAccountBindingOptions? CreateExternalAccountBinding()
    {
        if (string.IsNullOrEmpty(_options.ExternalAccountBinding?.KeyId) || string.IsNullOrEmpty(_options.ExternalAccountBinding?.HmacKey))
        {
            return null;
        }

        return AcmeExternalAccountBindingOptions.FromBase64Url(
            _options.ExternalAccountBinding.KeyId,
            _options.ExternalAccountBinding.HmacKey,
            _options.ExternalAccountBinding.Algorithm);
    }

    private string[] GetContacts() => [$"mailto:{_options.Contacts}"];
}
