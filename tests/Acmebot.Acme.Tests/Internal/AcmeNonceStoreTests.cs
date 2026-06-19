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
    public void Add_KeepsOnlyMostRecentNonce()
    {
        var store = new AcmeNonceStore();
        var older = Base64Url.EncodeToString([1]);
        var newer = Base64Url.EncodeToString([2]);

        store.Add(older);
        store.Add(newer);

        Assert.True(store.TryTake(out var nonce));
        Assert.Equal(newer, nonce);
        Assert.False(store.TryTake(out _));
    }

    [Fact]
    public void TryTake_ReturnsFalseWhenEmpty()
    {
        var store = new AcmeNonceStore();

        Assert.False(store.TryTake(out var nonce));
        Assert.Null(nonce);
    }
}
