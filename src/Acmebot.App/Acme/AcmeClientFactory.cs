using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Infrastructure;
using Acmebot.App.Options;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

public class AcmeClientFactory(IOptions<AcmebotOptions> options, IAcmeStateStore stateStore)
{
    private readonly AcmebotOptions _options = options.Value;

    public async Task<AcmeClientContext> CreateClientAsync()
    {
        var account = await stateStore.LoadAsync<AccountDetails>("account.json");
        var accountKey = await stateStore.LoadAsync<AccountKey>("account_key.json");
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
        var client = new AcmeClient(
            _options.Endpoint,
            new AcmeClientOptions
            {
                UserAgent = $"Acmebot/{Constants.ApplicationVersion}"
            });
        var directory = await client.GetDirectoryAsync();
        AcmeAccountHandle accountHandle;

        if (account is null)
        {
            var externalAccountBinding = CreateExternalAccountBinding();

            if (externalAccountBinding is null && (directory.Metadata?.ExternalAccountRequired ?? false))
            {
                throw new PreconditionException("This ACME endpoint requires External Account Binding (EAB). Configure EAB credentials and try again.");
            }

            accountHandle = await client.CreateAccountAsync(
                signer,
                new AcmeNewAccountRequest
                {
                    Contact = contacts,
                    TermsOfServiceAgreed = true
                },
                externalAccountBinding);
            account = AccountDetails.FromAccountHandle(accountHandle, directory.Metadata?.TermsOfService);

            if (isNewAccountKey)
            {
                await stateStore.SaveAsync(accountKey, "account_key.json");
            }

            await stateStore.SaveAsync(account, "account.json");
        }
        else
        {
            accountHandle = account.ToAccountHandle(signer);
        }

        if (!ContactsEqual(accountHandle.Account.Contact, contacts))
        {
            accountHandle = await client.UpdateAccountAsync(
                accountHandle,
                new AcmeUpdateAccountRequest
                {
                    Contact = contacts
                });
            account = AccountDetails.FromAccountHandle(accountHandle, directory.Metadata?.TermsOfService);

            await stateStore.SaveAsync(account, "account.json");
        }

        return new AcmeClientContext
        {
            Client = client,
            Directory = directory,
            Signer = signer,
            Account = accountHandle
        };
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

    private static bool ContactsEqual(IReadOnlyList<string>? actualContacts, IReadOnlyList<string> expectedContacts)
        => actualContacts is not null && actualContacts.SequenceEqual(expectedContacts, StringComparer.Ordinal);
}
