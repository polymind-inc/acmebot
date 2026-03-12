using Acmebot.Acme;
using Acmebot.Acme.Models;

namespace Acmebot.Internal;

public sealed class AcmeClientContext : IDisposable
{
    public required AcmeClient Client { get; init; }

    public required AcmeDirectoryResource Directory { get; init; }

    public required AcmeSigner Signer { get; init; }

    public required AcmeAccountHandle Account { get; init; }

    public void Dispose()
    {
        Signer.Dispose();
        Client.Dispose();
    }
}
