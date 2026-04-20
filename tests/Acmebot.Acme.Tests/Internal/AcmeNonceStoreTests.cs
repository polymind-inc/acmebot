using System.Buffers.Text;

using Acmebot.Acme.Internal;

using Xunit;

namespace Acmebot.Acme.Tests.Internal;

public sealed class AcmeNonceStoreTests
{
    [Fact]
    public void Add_IgnoresInvalidAndDuplicateNonces()
    {
        var store = new AcmeNonceStore();

        store.Add(null);
        store.Add(string.Empty);
        store.Add("not+valid");
        store.Add("bm9uY2Ux");
        store.Add("bm9uY2Ux");

        Assert.True(store.TryTake(out var nonce));
        Assert.Equal("bm9uY2Ux", nonce);
        Assert.False(store.TryTake(out _));
    }

    [Fact]
    public void Add_TrimsOldestNonceWhenCapacityExceeded()
    {
        var store = new AcmeNonceStore();
        var nonces = Enumerable.Range(0, 33)
            .Select(static value => Base64Url.EncodeToString([(byte)value]))
            .ToArray();

        foreach (var nonce in nonces)
        {
            store.Add(nonce);
        }

        var taken = new List<string>();

        while (store.TryTake(out var nonce))
        {
            taken.Add(nonce);
        }

        Assert.Equal(32, taken.Count);
        Assert.DoesNotContain(nonces[0], taken);
        Assert.Contains(nonces[^1], taken);
    }
}
