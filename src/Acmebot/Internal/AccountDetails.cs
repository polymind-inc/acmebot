using Acmebot.Acme;
using Acmebot.Acme.Models;

namespace Acmebot.Internal;

public sealed class AccountDetails
{
    public required AcmeAccountResource Payload { get; init; }

    public required Uri Kid { get; init; }

    public Uri? TosLink { get; init; }

    public AcmeAccountHandle ToAccountHandle(AcmeSigner signer)
    {
        ArgumentNullException.ThrowIfNull(signer);

        return new AcmeAccountHandle
        {
            AccountUrl = Kid,
            Signer = signer,
            Account = Payload
        };
    }

    public static AccountDetails FromAccountHandle(AcmeAccountHandle accountHandle, Uri? tosLink = null)
    {
        ArgumentNullException.ThrowIfNull(accountHandle);

        return new AccountDetails
        {
            Payload = accountHandle.Account,
            Kid = accountHandle.AccountUrl,
            TosLink = tosLink
        };
    }
}
