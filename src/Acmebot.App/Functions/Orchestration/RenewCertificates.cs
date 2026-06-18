using Acmebot.App.Services;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Acmebot.App.Functions.Orchestration;

public partial class RenewCertificates(
    CertificateQueryService certificateQueryService,
    ILogger<RenewCertificates> logger)
{
    [Function($"{nameof(RenewCertificates)}_{nameof(Timer)}")]
    public async Task Timer([TimerTrigger("0 0 0 * * *")] TimerInfo timer, [DurableClient] DurableTaskClient starter)
    {
        var started = 0;
        var running = 0;
        var skipped = 0;

        var certificates = await certificateQueryService.GetRenewalTargetsAsync();

        foreach (var certificate in certificates)
        {
            if (!certificate.IsRenewable)
            {
                skipped++;
                continue;
            }

            var instanceId = CertificateRenewalSchedulerOrchestrator.GetInstanceId(certificate.Name);

            var instance = await starter.GetInstanceAsync(instanceId, getInputsAndOutputs: false);

            if (!ShouldStartScheduler(instance))
            {
                running++;
                continue;
            }

            await starter.ScheduleNewOrchestrationInstanceAsync(
                nameof(CertificateRenewalSchedulerOrchestrator.ScheduleCertificateRenewal),
                certificate.Name,
                new StartOrchestrationOptions
                {
                    InstanceId = instanceId
                });

            started++;

            LogRenewalSchedulerStarted(logger, certificate.Name, instanceId);
        }

        LogRenewalSchedulersEnsured(logger, started, running, skipped);
    }

    private static bool ShouldStartScheduler(OrchestrationMetadata? instance)
    {
        return instance is null ||
               instance.RuntimeStatus is OrchestrationRuntimeStatus.Completed or
                   OrchestrationRuntimeStatus.Failed or
                   OrchestrationRuntimeStatus.Terminated;
    }

    [LoggerMessage(LogLevel.Information, "Certificate renewal scheduler started. CertificateName: {CertificateName}. InstanceId: {InstanceId}")]
    private static partial void LogRenewalSchedulerStarted(ILogger logger, string certificateName, string instanceId);

    [LoggerMessage(LogLevel.Information, "Certificate renewal schedulers ensured. Started: {Started}. Running: {Running}. Skipped: {Skipped}")]
    private static partial void LogRenewalSchedulersEnsured(ILogger logger, int started, int running, int skipped);
}
