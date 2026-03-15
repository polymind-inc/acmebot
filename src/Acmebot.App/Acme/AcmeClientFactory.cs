using System.Text.Json;

using Acmebot.Acme;
using Acmebot.Acme.Models;
using Acmebot.App.Infrastructure;
using Acmebot.App.Options;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

public class AcmeClientFactory(IOptions<AcmebotOptions> options)
{
    private readonly AcmebotOptions _options = options.Value;

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<AcmeClientContext> CreateClientAsync()
    {
        var account = LoadState<AccountDetails>("account.json");
        var accountKey = LoadState<AccountKey>("account_key.json");
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

            SaveState(account, "account.json");

            if (isNewAccountKey)
            {
                SaveState(accountKey, "account_key.json");
            }
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

            SaveState(account, "account.json");
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

    private TState? LoadState<TState>(string path)
    {
        var fullPath = ResolveStateFullPath(path);

        if (!File.Exists(fullPath))
        {
            return default;
        }

        var json = File.ReadAllText(fullPath);

        return JsonSerializer.Deserialize<TState>(json, s_jsonSerializerOptions);
    }

    private void SaveState<TState>(TState value, string path)
    {
        var fullPath = ResolveStateFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(value, s_jsonSerializerOptions);

        File.WriteAllText(fullPath, json);
    }

    private string ResolveStateFullPath(string path) => Environment.ExpandEnvironmentVariables($"%HOME%/data/.acmebot/{_options.Endpoint.Host}/{path}");
}
