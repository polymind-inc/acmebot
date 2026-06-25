using Acmebot.App.Models;

using Microsoft.DurableTask.Client;

namespace Acmebot.App.Services;

internal sealed record CertificateRenewalScheduleSnapshot(
    CertificateRenewalSchedulerStatus? Status,
    OrchestrationRuntimeStatus RuntimeStatus,
    string? FailureMessage,
    DateTimeOffset LastUpdatedAt);
