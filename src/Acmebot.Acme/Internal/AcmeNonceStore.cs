using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;

namespace Acmebot.Acme.Internal;

internal sealed class AcmeNonceStore
{
    // ACME nonces are single-use and expire server-side, so only the most recently received one is
    // worth keeping. Holding just the latest nonce guarantees a request always uses the freshest one
    // and a badNonce retry naturally picks up the fresh nonce returned by the error response.
    private string? _nonce;

    public void Add(string? nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce) || !Base64Url.IsValid(nonce))
        {
            return;
        }

        Interlocked.Exchange(ref _nonce, nonce);
    }

    public bool TryTake([NotNullWhen(true)] out string? nonce)
    {
        nonce = Interlocked.Exchange(ref _nonce, null);

        return nonce is not null;
    }
}
