using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Models;
using Acmebot.App.Providers;

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

        LogCertificateIssuanceStarted(logger, certificatePolicyItem.CertificateName, string.Join(",", certificatePolicyItem.DnsNames));

        try
        {
            // 前提条件をチェック
            var zones = await context.CallDns01PreconditionAsync(certificatePolicyItem);

            // 新しく ACME Order を作成する (ARI で更新扱いにする場合は既存証明書の Certificate ID を replaces に指定)
            var orderDetails = await context.CallOrderAsync((certificatePolicyItem.DnsNames, certificatePolicyItem.CertificateId, certificatePolicyItem.Profile));

            // 既に確認済みの場合は Challenge をスキップする
            if (orderDetails.Payload.Status != AcmeOrderStatuses.Ready)
            {
                await ValidateOrderAsync(context, certificatePolicyItem, orderDetails, zones, logger);
            }

            // Key Vault で CSR を作成し Finalize を実行
            orderDetails = await context.CallFinalizeOrderAsync((certificatePolicyItem, orderDetails));

            // Finalize の時点でステータスが valid の時点はスキップ
            if (orderDetails.Payload.Status != AcmeOrderStatuses.Valid)
            {
                // Wait for the finalized order to become valid using bounded exponential backoff.
                orderDetails = await context.CallCheckIsValidAsync(orderDetails, TaskOptions.FromRetryPolicy(PollingRetryPolicy));
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

    private static async Task ValidateOrderAsync(
        TaskOrchestrationContext context,
        CertificatePolicyItem certificatePolicyItem,
        OrderDetails orderDetails,
        IReadOnlyList<DnsZone> zones,
        ILogger logger)
    {
        IReadOnlyList<AcmeChallengeResult> challengeResults = [];
        var propagationSeconds = 0;

        if (orderDetails.Payload.Authorizations.Count > 0)
        {
            (challengeResults, propagationSeconds) = await context.CallDns01AuthorizationAsync((certificatePolicyItem.DnsProviderName, certificatePolicyItem.DnsAlias, orderDetails.Payload.Authorizations, zones));
        }

        if (challengeResults.Count == 0)
        {
            // Valid authorizations can precede the order's transition to Ready.
            await context.CallCheckIsReadyAsync((orderDetails, challengeResults), TaskOptions.FromRetryPolicy(PollingRetryPolicy));
            return;
        }

        try
        {
            LogDnsChallengePropagationDelay(logger, certificatePolicyItem.CertificateName, propagationSeconds);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(propagationSeconds), CancellationToken.None);
            await context.CallCheckDnsChallengeAsync(challengeResults, TaskOptions.FromRetryPolicy(PollingRetryPolicy));
            await context.CallAnswerChallengesAsync(challengeResults);
            await context.CallCheckIsReadyAsync((orderDetails, challengeResults), TaskOptions.FromRetryPolicy(PollingRetryPolicy));
        }
        finally
        {
            await context.CallCleanupDnsChallengeAsync((certificatePolicyItem.DnsProviderName, challengeResults, zones));
        }
    }

    // Allow slow DNS propagation and CA processing without increasing the number of queries.
    // Twelve attempts wait for a total of 8 minutes 15 seconds, excluding activity execution time.
    internal static RetryPolicy PollingRetryPolicy { get; } = new(
        maxNumberOfAttempts: 12,
        firstRetryInterval: TimeSpan.FromSeconds(5),
        backoffCoefficient: 2,
        maxRetryInterval: TimeSpan.FromMinutes(1))
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
