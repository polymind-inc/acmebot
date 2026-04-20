using System.Buffers.Text;
using System.Security.Cryptography;

using Xunit;

namespace Acmebot.Acme.Tests;

public sealed class AcmeSignerTests
{
    [Fact]
    public void CreateP256_ExportsExpectedEcJwk()
    {
        using var signer = AcmeSigner.CreateP256();

        var jwk = signer.ExportJsonWebKey();

        Assert.Equal("ES256", signer.Algorithm);
        Assert.Equal("EC", jwk.KeyType);
        Assert.Equal("P-256", jwk.Curve);
        Assert.True(Base64Url.IsValid(jwk.X));
        Assert.True(Base64Url.IsValid(jwk.Y));
        Assert.StartsWith("{\"crv\":\"P-256\",\"kty\":\"EC\"", jwk.ToThumbprintJson(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(signer.GetThumbprint()));
    }

    [Fact]
    public void CreateRsa_ExportsExpectedRsaJwk()
    {
        using var rsa = RSA.Create(2048);
        using var signer = AcmeSigner.Create(rsa);

        var jwk = signer.ExportJsonWebKey();

        Assert.Equal("RS256", signer.Algorithm);
        Assert.Equal("RSA", jwk.KeyType);
        Assert.True(Base64Url.IsValid(jwk.Modulus));
        Assert.True(Base64Url.IsValid(jwk.Exponent));
        Assert.StartsWith("{\"e\":\"", jwk.ToThumbprintJson(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(signer.GetThumbprint()));
    }

    [Fact]
    public void CreateRsa_ThrowsForUnsupportedHashAlgorithm()
    {
        using var rsa = RSA.Create(2048);

        var exception = Assert.Throws<NotSupportedException>(() => AcmeSigner.Create(rsa, HashAlgorithmName.MD5));

        Assert.Equal("Only SHA-256, SHA-384, and SHA-512 RSA signatures are supported.", exception.Message);
    }

    [Fact]
    public void Members_ThrowWhenSignerIsUsedAfterDispose()
    {
        var signer = AcmeSigner.CreateP256();
        signer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => signer.GetThumbprint());
        Assert.Throws<ObjectDisposedException>(() => signer.SignData("payload"u8));
    }
}
