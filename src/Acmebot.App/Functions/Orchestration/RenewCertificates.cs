using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Orchestration;

public partial class RenewCertificates(ILogger<RenewCertificates> logger)
{
    [Function($"{nameof(RenewCertificates)}_{nameof(Orchestrator)}")]
    public async Task Orchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // 更新が必要な証明書の一覧を取得する
        var certificates = await context.CallGetRenewalCertificatesAsync(null!);

        // 更新対象となる証明書がない場合は終わる
        if (certificates.Count == 0)
        {
            LogCertificatesNotFound(logger);

            return;
        }

        // スロットリング対策として 600 秒以内でジッターを追加する
        var jitter = (uint)context.NewGuid().GetHashCode() % 600;

        LogAddingRandomDelay(logger, jitter);

        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(jitter), CancellationToken.None);

        // 証明書の更新を行う
        foreach (var certificate in certificates)
        {
            LogRenewingCertificate(logger, certificate.Name, certificate.ExpiresOn);

            try
            {
                // 証明書の更新処理を開始
                var certificatePolicyItem = await context.CallGetCertificatePolicyAsync(certificate.Name);

                await context.CallSubOrchestratorAsync(nameof(CertificateIssuanceOrchestrator.IssueCertificate), certificatePolicyItem, TaskOptions.FromRetryPolicy(_retryOptions));
            }
            catch (Exception ex)
            {
                // 失敗した場合はログに詳細を書き出して続きを実行する
                LogFailedSubOrchestration(logger, ex, certificate.Name, string.Join(",", certificate.DnsNames));
            }
        }
    }

    [Function($"{nameof(RenewCertificates)}_{nameof(Timer)}")]
    public async Task Timer([TimerTrigger("0 0 0 * * *")] TimerInfo timer, [DurableClient] DurableTaskClient starter)
    {
        // Function input comes from the request content.
        var instanceId = await starter.ScheduleNewOrchestrationInstanceAsync($"{nameof(RenewCertificates)}_{nameof(Orchestrator)}");

        LogOrchestrationStarted(logger, instanceId);
    }

    private readonly RetryPolicy _retryOptions = new(2, TimeSpan.FromHours(3))
    {
        HandleFailure = taskFailureDetails => taskFailureDetails.IsCausedBy<RetriableOrchestratorException>()
    };

    [LoggerMessage(LogLevel.Information, "Scheduled certificate renewal skipped. Reason: No certificates are due for renewal.")]
    private static partial void LogCertificatesNotFound(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Scheduled certificate renewal delayed. DelaySeconds: {Jitter}")]
    private static partial void LogAddingRandomDelay(ILogger logger, uint jitter);

    [LoggerMessage(LogLevel.Information, "Scheduled certificate renewal processing. CertificateName: {CertificateName}. ExpiresOn: {CertificateExpiresOn}")]
    private static partial void LogRenewingCertificate(ILogger logger, string certificateName, DateTimeOffset certificateExpiresOn);

    [LoggerMessage(LogLevel.Error, "Scheduled certificate renewal failed. CertificateName: {CertificateName}. DnsNames: {DnsNames}")]
    private static partial void LogFailedSubOrchestration(ILogger logger, Exception exception, string certificateName, string dnsNames);

    [LoggerMessage(LogLevel.Information, "Scheduled certificate renewal orchestration started. InstanceId: {InstanceId}")]
    private static partial void LogOrchestrationStarted(ILogger logger, string instanceId);
}
