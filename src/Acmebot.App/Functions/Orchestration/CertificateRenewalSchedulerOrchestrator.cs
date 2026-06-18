using System.Security.Cryptography;
using System.Text;

using Acmebot.App.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Orchestration;

public partial class CertificateRenewalSchedulerOrchestrator
{
    public static string GetInstanceId(string certificateName) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(certificateName)));


    [Function(nameof(ScheduleCertificateRenewal))]
    public async Task ScheduleCertificateRenewal([OrchestrationTrigger] TaskOrchestrationContext context, string certificateName)
    {
        var logger = context.CreateReplaySafeLogger<CertificateRenewalSchedulerOrchestrator>();

        SetSchedulerStatus(context, certificateName, "Checking", null, "Automatic renewal status is being refreshed.");

        var evaluation = await context.CallEvaluateCertificateRenewalAsync(certificateName);

        if (!evaluation.IsActive)
        {
            SetSchedulerStatus(context, certificateName, "Stopped", null, evaluation.Reason);
            LogCertificateRenewalSchedulerStopped(logger, certificateName, evaluation.Reason);
            return;
        }

        if (evaluation.ShouldRenew)
        {
            SetSchedulerStatus(context, certificateName, "Renewing", null, evaluation.Reason);

            try
            {
                LogCertificateRenewalStarted(logger, certificateName, evaluation.Reason);

                var certificatePolicyItem = await context.CallGetCertificatePolicyAsync(certificateName);

                await context.CallSubOrchestratorAsync(
                    nameof(CertificateIssuanceOrchestrator.IssueCertificate),
                    certificatePolicyItem,
                    TaskOptions.FromRetryPolicy(_retryOptions));
            }
            catch (Exception ex)
            {
                LogCertificateRenewalFailed(logger, ex, certificateName);

                var nextCheck = context.CurrentUtcDateTime.Add(s_failedRenewalRetryInterval);
                SetSchedulerStatus(context, certificateName, "Retrying", nextCheck, "Automatic renewal failed. Retrying later.");

                await context.CreateTimer(nextCheck, CancellationToken.None);
                context.ContinueAsNew(certificateName);

                return;
            }

            context.ContinueAsNew(certificateName);

            return;
        }

        LogCertificateRenewalScheduled(logger, certificateName, evaluation.NextCheck, evaluation.Reason);

        SetSchedulerStatus(context, certificateName, "Scheduled", evaluation.NextCheck, evaluation.Reason);

        await context.CreateTimer(evaluation.NextCheck.UtcDateTime, CancellationToken.None);

        context.ContinueAsNew(certificateName);
    }

    private static readonly TimeSpan s_failedRenewalRetryInterval = TimeSpan.FromHours(6);

    private readonly RetryPolicy _retryOptions = new(2, TimeSpan.FromHours(3))
    {
        HandleFailure = taskFailureDetails => taskFailureDetails.IsCausedBy<RetriableOrchestratorException>()
    };

    private static void SetSchedulerStatus(TaskOrchestrationContext context, string certificateName, string state, DateTimeOffset? nextCheck, string reason)
    {
        context.SetCustomStatus(new CertificateRenewalSchedulerStatus
        {
            CertificateName = certificateName,
            State = state,
            NextCheck = nextCheck,
            Reason = reason,
            UpdatedAt = context.CurrentUtcDateTime
        });
    }

    [LoggerMessage(LogLevel.Information, "Certificate renewal scheduler stopped. CertificateName: {CertificateName}. Reason: {Reason}")]
    private static partial void LogCertificateRenewalSchedulerStopped(ILogger logger, string certificateName, string reason);

    [LoggerMessage(LogLevel.Information, "Automatic certificate renewal started. CertificateName: {CertificateName}. Reason: {Reason}")]
    private static partial void LogCertificateRenewalStarted(ILogger logger, string certificateName, string reason);

    [LoggerMessage(LogLevel.Information, "Automatic certificate renewal scheduled. CertificateName: {CertificateName}. NextCheck: {NextCheck}. Reason: {Reason}")]
    private static partial void LogCertificateRenewalScheduled(ILogger logger, string certificateName, DateTimeOffset nextCheck, string reason);

    [LoggerMessage(LogLevel.Error, "Automatic certificate renewal failed. CertificateName: {CertificateName}")]
    private static partial void LogCertificateRenewalFailed(ILogger logger, Exception exception, string certificateName);
}
