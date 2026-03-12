using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

using Acmebot.Acme.Internal;

namespace Acmebot.Acme;

public sealed class AcmeSigner : IDisposable
{
    private readonly ECDsa? _ecdsa;
    private readonly RSA? _rsa;
    private readonly HashAlgorithmName _hashAlgorithm;
    private readonly bool _ownsKey;
    private bool _disposed;

    private AcmeSigner(ECDsa ecdsa, string algorithm, HashAlgorithmName hashAlgorithm, bool ownsKey)
    {
        _ecdsa = ecdsa;
        _hashAlgorithm = hashAlgorithm;
        _ownsKey = ownsKey;
        Algorithm = algorithm;
    }

    private AcmeSigner(RSA rsa, string algorithm, HashAlgorithmName hashAlgorithm, bool ownsKey)
    {
        _rsa = rsa;
        _hashAlgorithm = hashAlgorithm;
        _ownsKey = ownsKey;
        Algorithm = algorithm;
    }

    public string Algorithm { get; }

    public static AcmeSigner Create(ECDsa ecdsa, bool ownsKey = false)
    {
        ArgumentNullException.ThrowIfNull(ecdsa);

        var curveOid = ecdsa.ExportParameters(false).Curve.Oid.Value;

        return curveOid switch
        {
            "1.2.840.10045.3.1.7" => new AcmeSigner(ecdsa, "ES256", HashAlgorithmName.SHA256, ownsKey),
            "1.3.132.0.34" => new AcmeSigner(ecdsa, "ES384", HashAlgorithmName.SHA384, ownsKey),
            "1.3.132.0.35" => new AcmeSigner(ecdsa, "ES512", HashAlgorithmName.SHA512, ownsKey),
            _ => throw new NotSupportedException("Only NIST P-256, P-384, and P-521 ECDSA keys are supported.")
        };
    }

    public static AcmeSigner Create(RSA rsa, HashAlgorithmName? hashAlgorithm = null, bool ownsKey = false)
    {
        ArgumentNullException.ThrowIfNull(rsa);

        var resolvedHashAlgorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
        var algorithm = resolvedHashAlgorithm.Name switch
        {
            nameof(HashAlgorithmName.SHA256) => "RS256",
            nameof(HashAlgorithmName.SHA384) => "RS384",
            nameof(HashAlgorithmName.SHA512) => "RS512",
            _ => throw new NotSupportedException("Only SHA-256, SHA-384, and SHA-512 RSA signatures are supported.")
        };

        return new AcmeSigner(rsa, algorithm, resolvedHashAlgorithm, ownsKey);
    }

    public static AcmeSigner CreateP256() => Create(ECDsa.Create(ECCurve.NamedCurves.nistP256), ownsKey: true);

    public byte[] SignData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_ecdsa is not null)
        {
            return _ecdsa.SignData(data, _hashAlgorithm, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        return _rsa!.SignData(data, _hashAlgorithm, RSASignaturePadding.Pkcs1);
    }

    public string GetThumbprint()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var jwk = ExportJsonWebKey();
        var jsonBytes = Encoding.UTF8.GetBytes(jwk.ToThumbprintJson());
        var hash = SHA256.HashData(jsonBytes);

        return Base64Url.EncodeToString(hash);
    }

    internal AcmeJsonWebKey ExportJsonWebKey()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_ecdsa is not null)
        {
            var parameters = _ecdsa.ExportParameters(false);

            var curveName = parameters.Curve.Oid.Value switch
            {
                "1.2.840.10045.3.1.7" => "P-256",
                "1.3.132.0.34" => "P-384",
                "1.3.132.0.35" => "P-521",
                _ => throw new NotSupportedException("Unsupported elliptic curve.")
            };

            return new AcmeJsonWebKey
            {
                KeyType = "EC",
                Curve = curveName,
                X = Base64Url.EncodeToString(parameters.Q.X),
                Y = Base64Url.EncodeToString(parameters.Q.Y)
            };
        }

        var rsaParameters = _rsa!.ExportParameters(false);

        return new AcmeJsonWebKey
        {
            KeyType = "RSA",
            Modulus = Base64Url.EncodeToString(TrimUnsignedBigEndian(rsaParameters.Modulus)),
            Exponent = Base64Url.EncodeToString(TrimUnsignedBigEndian(rsaParameters.Exponent))
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsKey)
        {
            _ecdsa?.Dispose();
            _rsa?.Dispose();
        }
    }

    private static byte[] TrimUnsignedBigEndian(byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return [];
        }

        var index = 0;

        while (index < value.Length - 1 && value[index] == 0)
        {
            index++;
        }

        return index == 0 ? value : value[index..];
    }
}
