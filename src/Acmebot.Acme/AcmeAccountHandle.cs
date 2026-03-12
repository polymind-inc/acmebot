using Acmebot.Acme.Models;

namespace Acmebot.Acme;

public sealed class AcmeAccountHandle
{
    public required Uri AccountUrl { get; init; }

    public required AcmeSigner Signer { get; init; }

    public required AcmeAccountResource Account { get; init; }
}
