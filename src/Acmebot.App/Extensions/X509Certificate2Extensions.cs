using System.Buffers.Text;
using System.Security.Cryptography.X509Certificates;

namespace Acmebot.App.Extensions;

internal static class X509Certificate2Extensions
{
    public static string? GetCertificateId(this X509Certificate2 x509Certificate2)
    {
        var keyIdentifierExtension = x509Certificate2.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().FirstOrDefault();

        if (keyIdentifierExtension?.KeyIdentifier is null)
        {
            return null;
        }

        var keyIdentifier = Base64Url.EncodeToString(keyIdentifierExtension.KeyIdentifier.Value.Span);
        var serialNumber = Base64Url.EncodeToString(x509Certificate2.SerialNumberBytes.Span);

        return $"{keyIdentifier}.{serialNumber}";
    }
}
