using Acmebot.Acme.Models;
using Acmebot.App.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Orchestration;

public partial class CertificateIssuanceOrchestrator
{
    [Function(nameof(IssueCertificate))]
    public async Task IssueCertificate([OrchestrationTrigger] TaskOrchestrationContext context, CertificatePolicyItem certificatePolicyItem)
    {
        var logger = context.CreateReplaySafeLogger<CertificateIssuanceOrchestrator>();

        if (string.IsNullOrEmpty(certificatePolicyItem.CertificateName))
        {
            return;
        }

        LogCertificateIssuanceStarted(logger, certificatePolicyItem.CertificateName, string.Join(",", certificatePolicyItem.DnsNames));

        try
        {
            // 前提条件をチェック
            certificatePolicyItem.DnsProviderName = await context.CallDns01PreconditionAsync(certificatePolicyItem);

            // 新しく ACME Order を作成する
            var orderDetails = await context.CallOrderAsync(certificatePolicyItem.DnsNames);

            // 既に確認済みの場合は Challenge をスキップする
            if (orderDetails.Payload.Status != AcmeOrderStatuses.Ready)
            {
                // ACME DNS-01 Challenge を実行
                var (challengeResults, propagationSeconds) = await context.CallDns01AuthorizationAsync((certificatePolicyItem.DnsProviderName, certificatePolicyItem.DnsAlias, orderDetails.Payload.Authorizations));

                // DNS Provider が指定した分だけ後続の処理を遅延させる
                LogDnsChallengePropagationDelay(logger, certificatePolicyItem.CertificateName, propagationSeconds);

                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(propagationSeconds), CancellationToken.None);

                // 正しく追加した DNS TXT レコードが引けるか確認
                await context.CallCheckDnsChallengeAsync(challengeResults, TaskOptions.FromRetryPolicy(_retryPolicy));

                // ACME Answer Challenge を実行
                await context.CallAnswerChallengesAsync(challengeResults);

                // ACME Order のステータスが ready になるまで 60 秒待機
                await context.CallCheckIsReadyAsync((orderDetails, challengeResults), TaskOptions.FromRetryPolicy(_retryPolicy));

                // 作成した DNS レコードを削除
                await context.CallCleanupDnsChallengeAsync((certificatePolicyItem.DnsProviderName, challengeResults));
            }

            // Key Vault で CSR を作成し Finalize を実行
            orderDetails = await context.CallFinalizeOrderAsync((certificatePolicyItem, orderDetails));

            // Finalize の時点でステータスが valid の時点はスキップ
            if (orderDetails.Payload.Status != AcmeOrderStatuses.Valid)
            {
                // Finalize 後のステータスが valid になるまで 60 秒待機
                orderDetails = await context.CallCheckIsValidAsync(orderDetails, TaskOptions.FromRetryPolicy(_retryPolicy));
            }

            // 証明書をダウンロードし Key Vault に保存された秘密鍵とマージ
            var certificate = await context.CallMergeCertificateAsync((certificatePolicyItem.CertificateName, orderDetails));

            // 証明書の更新が完了後に Webhook を送信する
            await context.CallSendCompletedEventAsync((certificate.Name, certificate.ExpiresOn, certificatePolicyItem.DnsNames));

            LogCertificateIssuanceCompleted(logger, certificate.Name, certificate.ExpiresOn);
        }
        catch (Exception ex)
        {
            LogCertificateIssuanceFailed(logger, ex, certificatePolicyItem.CertificateName, string.Join(",", certificatePolicyItem.DnsNames));

            await context.CallSendFailedEventAsync((certificatePolicyItem.CertificateName, certificatePolicyItem.DnsNames));

            throw;
        }
    }

    private readonly RetryPolicy _retryPolicy = new(12, TimeSpan.FromSeconds(5))
    {
        HandleFailure = taskFailureDetails => taskFailureDetails.IsCausedBy<RetriableActivityException>()
    };

    [LoggerMessage(LogLevel.Information, "Certificate issuance orchestration started. CertificateName: {CertificateName}. DnsNames: {DnsNames}")]
    private static partial void LogCertificateIssuanceStarted(ILogger logger, string certificateName, string dnsNames);

    [LoggerMessage(LogLevel.Information, "DNS challenge propagation wait started. CertificateName: {CertificateName}. DelaySeconds: {DelaySeconds}")]
    private static partial void LogDnsChallengePropagationDelay(ILogger logger, string certificateName, long delaySeconds);

    [LoggerMessage(LogLevel.Information, "Certificate issuance completed. CertificateName: {CertificateName}. ExpiresOn: {ExpiresOn}")]
    private static partial void LogCertificateIssuanceCompleted(ILogger logger, string certificateName, DateTimeOffset expiresOn);

    [LoggerMessage(LogLevel.Error, "Certificate issuance failed. CertificateName: {CertificateName}. DnsNames: {DnsNames}")]
    private static partial void LogCertificateIssuanceFailed(ILogger logger, Exception exception, string certificateName, string dnsNames);
}
