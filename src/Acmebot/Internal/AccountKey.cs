using System.Security.Cryptography;
using System.Text.Json;

using Acmebot.Acme;

namespace Acmebot.Internal;

internal class AccountKey
{
    public required string KeyType { get; set; }
    public required string KeyExport { get; set; }

    public static AccountKey CreateDefault()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return new AccountKey
        {
            KeyType = "ES256",
            KeyExport = ecdsa.ExportPkcs8PrivateKeyPem()
        };
    }

    public AcmeSigner GenerateSigner()
    {
        if (KeyType.StartsWith("ES", StringComparison.Ordinal))
        {
            var ecdsa = ECDsa.Create();

            if (IsLegacyEcExport())
            {
                ImportLegacyEcKey(ecdsa);
            }
            else
            {
                ecdsa.ImportFromPem(KeyExport);
            }

            return AcmeSigner.Create(ecdsa, ownsKey: true);
        }

        if (KeyType.StartsWith("RS", StringComparison.Ordinal))
        {
            var rsa = RSA.Create();

            if (KeyExport.TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                rsa.FromXmlString(KeyExport);
            }
            else
            {
                rsa.ImportFromPem(KeyExport);
            }

            return AcmeSigner.Create(rsa, ParseHashAlgorithm(), ownsKey: true);
        }

        throw new Exception($"Unknown or unsupported KeyType [{KeyType}]");
    }

    private bool IsLegacyEcExport()
    {
        return KeyExport.TrimStart().StartsWith("{", StringComparison.Ordinal);
    }

    private void ImportLegacyEcKey(ECDsa ecdsa)
    {
        var details = JsonSerializer.Deserialize<LegacyEcExportDetails>(KeyExport) ?? throw new InvalidOperationException("Invalid EC account key format.");

        ecdsa.ImportParameters(new ECParameters
        {
            Curve = GetCurve(details.HashSize),
            D = Convert.FromBase64String(details.D),
            Q = new ECPoint
            {
                X = Convert.FromBase64String(details.X),
                Y = Convert.FromBase64String(details.Y)
            }
        });
    }

    private static ECCurve GetCurve(int hashSize)
    {
        return hashSize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            512 => ECCurve.NamedCurves.nistP521,
            _ => throw new NotSupportedException($"Unknown or unsupported EC hash size [{hashSize}]")
        };
    }

    private HashAlgorithmName ParseHashAlgorithm()
    {
        return KeyType switch
        {
            "RS256" => HashAlgorithmName.SHA256,
            "RS384" => HashAlgorithmName.SHA384,
            "RS512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Unknown or unsupported RSA key type [{KeyType}]")
        };
    }

    private sealed class LegacyEcExportDetails
    {
        public required int HashSize { get; init; }

        public required string D { get; init; }

        public required string X { get; init; }

        public required string Y { get; init; }
    }
}
