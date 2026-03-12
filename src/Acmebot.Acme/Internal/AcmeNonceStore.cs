using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Acmebot.Acme.Internal;

internal sealed class AcmeNonceStore
{
    private const int MaxNonceCount = 32;

    private readonly ConcurrentQueue<string> _nonces = new();
    private readonly ConcurrentDictionary<string, byte> _nonceSet = new(StringComparer.Ordinal);

    public void Add(string? nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce) || !Base64Url.IsValid(nonce))
        {
            return;
        }

        if (!_nonceSet.TryAdd(nonce, 0))
        {
            return;
        }

        _nonces.Enqueue(nonce);
        Trim();
    }

    public bool TryTake([NotNullWhen(true)] out string? nonce)
    {
        while (_nonces.TryDequeue(out nonce))
        {
            if (_nonceSet.TryRemove(nonce, out _))
            {
                return true;
            }
        }

        nonce = null;
        return false;
    }

    private void Trim()
    {
        while (_nonceSet.Count > MaxNonceCount && _nonces.TryDequeue(out var discardedNonce))
        {
            _nonceSet.TryRemove(discardedNonce, out _);
        }
    }
}
