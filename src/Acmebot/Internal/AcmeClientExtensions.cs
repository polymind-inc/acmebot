using System.Security.Cryptography.X509Certificates;

using Acmebot.Acme;

namespace Acmebot.Internal;

internal static class AcmeClientExtensions
{
    public static async Task<X509Certificate2Collection> GetOrderCertificateAsync(
        this AcmeClient acmeClient,
        AcmeAccountHandle account,
        OrderDetails order,
        string? preferredChain,
        CancellationToken cancel = default)
    {
        if (order.Payload.Certificate is null)
        {
            throw new InvalidOperationException("The ACME order does not include a certificate URL.");
        }

        var defaultCertificateChain = await acmeClient.DownloadCertificateAsync(account, order.Payload.Certificate, cancel);
        var defaultX509Certificates = new X509Certificate2Collection();
        defaultX509Certificates.AddRange(defaultCertificateChain.Certificates.ToArray());

        // 証明書チェーンが未指定の場合は即返す
        if (string.IsNullOrEmpty(preferredChain))
        {
            return defaultX509Certificates;
        }

        foreach (var certificateUrl in defaultCertificateChain.AlternateCertificateUrls)
        {
            // 代替の証明書をダウンロードする
            var certificateChain = await acmeClient.DownloadCertificateAsync(account, certificateUrl, cancel);
            var x509Certificates = new X509Certificate2Collection();
            x509Certificates.AddRange(certificateChain.Certificates.ToArray());

            // ルート CA の名前が指定された証明書チェーンに一致する場合は返す
            if (x509Certificates[^1].GetNameInfo(X509NameType.DnsName, true) == preferredChain)
            {
                return x509Certificates;
            }
        }

        // マッチする証明書チェーンが存在しない場合はデフォルトを返す
        return defaultX509Certificates;
    }
}
