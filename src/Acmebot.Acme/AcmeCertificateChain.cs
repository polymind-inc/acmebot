using System.Security.Cryptography.X509Certificates;

namespace Acmebot.Acme;

public sealed class AcmeCertificateChain
{
    public required string PemChain { get; init; }

    public required IReadOnlyList<X509Certificate2> Certificates { get; init; }

    public required IReadOnlyList<Uri> AlternateCertificateUrls { get; init; }

    public required IReadOnlyList<Uri> IssuerCertificateUrls { get; init; }
}
