using Acmebot.App.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Orchestration;

public partial class CertificateRenewalSchedulerOrchestrator
{
    [Function(nameof(ScheduleCertificateRenewal))]
    public async Task ScheduleCertificateRenewal([OrchestrationTrigger] TaskOrchestrationContext context, CertificateRenewalSchedulerState state)
    {
        var logger = context.CreateReplaySafeLogger<CertificateRenewalSchedulerOrchestrator>();

        var evaluation = await context.CallEvaluateCertificateRenewalAsync(state);

        if (!evaluation.IsActive)
        {
            LogCertificateRenewalSchedulerStopped(logger, state.CertificateName, evaluation.Reason);
            return;
        }

        if (evaluation.ShouldRenew)
        {
            try
            {
                LogCertificateRenewalStarted(logger, state.CertificateName, evaluation.Reason);

                var certificatePolicyItem = await context.CallGetCertificatePolicyAsync(state.CertificateName);

                await context.CallSubOrchestratorAsync(
                    nameof(CertificateIssuanceOrchestrator.IssueCertificate),
                    certificatePolicyItem,
                    TaskOptions.FromRetryPolicy(_retryOptions));
            }
            catch (Exception ex)
            {
                LogCertificateRenewalFailed(logger, ex, state.CertificateName);

                await context.CreateTimer(context.CurrentUtcDateTime.Add(s_failedRenewalRetryInterval), CancellationToken.None);
                context.ContinueAsNew(state);

                return;
            }

            context.ContinueAsNew(state);

            return;
        }

        LogCertificateRenewalScheduled(logger, state.CertificateName, evaluation.NextCheck, evaluation.Reason);

        await context.CreateTimer(evaluation.NextCheck.UtcDateTime, CancellationToken.None);

        context.ContinueAsNew(state);
    }

    private static readonly TimeSpan s_failedRenewalRetryInterval = TimeSpan.FromHours(6);

    private readonly RetryPolicy _retryOptions = new(2, TimeSpan.FromHours(3))
    {
        HandleFailure = taskFailureDetails => taskFailureDetails.IsCausedBy<RetriableOrchestratorException>()
    };

    [LoggerMessage(LogLevel.Information, "Certificate renewal scheduler stopped. CertificateName: {CertificateName}. Reason: {Reason}")]
    private static partial void LogCertificateRenewalSchedulerStopped(ILogger logger, string certificateName, string reason);

    [LoggerMessage(LogLevel.Information, "Automatic certificate renewal started. CertificateName: {CertificateName}. Reason: {Reason}")]
    private static partial void LogCertificateRenewalStarted(ILogger logger, string certificateName, string reason);

    [LoggerMessage(LogLevel.Information, "Automatic certificate renewal scheduled. CertificateName: {CertificateName}. NextCheck: {NextCheck}. Reason: {Reason}")]
    private static partial void LogCertificateRenewalScheduled(ILogger logger, string certificateName, DateTimeOffset nextCheck, string reason);

    [LoggerMessage(LogLevel.Error, "Automatic certificate renewal failed. CertificateName: {CertificateName}")]
    private static partial void LogCertificateRenewalFailed(ILogger logger, Exception exception, string certificateName);
}
