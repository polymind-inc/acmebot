using System.Security.Cryptography;
using System.Text;

using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Options;

using Azure.Security.KeyVault.Certificates;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmebot.App.Functions.Orchestration;

public partial class RenewCertificates(
    CertificateClient certificateClient,
    IOptions<AcmebotOptions> options,
    ILogger<RenewCertificates> logger)
{
    private readonly AcmebotOptions _options = options.Value;

    [Function($"{nameof(RenewCertificates)}_{nameof(Timer)}")]
    public async Task Timer([TimerTrigger("0 0 0 * * *")] TimerInfo timer, [DurableClient] DurableTaskClient starter)
    {
        var started = 0;
        var running = 0;
        var skipped = 0;

        await foreach (var properties in certificateClient.GetPropertiesOfCertificatesAsync())
        {
            if (properties.Enabled != true || !properties.IsIssuedByAcmebot() || !properties.IsSameEndpoint(_options.Endpoint))
            {
                skipped++;
                continue;
            }

            var instanceId = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(properties.Name)));

            var instance = await starter.GetInstanceAsync(instanceId, getInputsAndOutputs: false);

            if (!ShouldStartScheduler(instance))
            {
                running++;
                continue;
            }

            await starter.ScheduleNewOrchestrationInstanceAsync(
                nameof(CertificateRenewalSchedulerOrchestrator.ScheduleCertificateRenewal),
                new CertificateRenewalSchedulerState
                {
                    CertificateName = properties.Name
                },
                new StartOrchestrationOptions
                {
                    InstanceId = instanceId
                });

            started++;

            LogRenewalSchedulerStarted(logger, properties.Name, instanceId);
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

    [LoggerMessage(LogLevel.Information, "Certificate renewal scheduler started. CertificateName: {CertificateName}. InstanceId: {instanceId}")]
    private static partial void LogRenewalSchedulerStarted(ILogger logger, string certificateName, string instanceId);

    [LoggerMessage(LogLevel.Information, "Certificate renewal schedulers ensured. Started: {Started}. Running: {Running}. Skipped: {Skipped}")]
    private static partial void LogRenewalSchedulersEnsured(ILogger logger, int started, int running, int skipped);
}
