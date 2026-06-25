using Acmebot.App.Models;
using Acmebot.App.Services;

using Microsoft.DurableTask.Client;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateRenewalStateEvaluatorTests
{
    private static readonly DateTimeOffset s_lastUpdatedAt = new(2026, 6, 24, 1, 2, 3, TimeSpan.Zero);
    private static readonly DateTimeOffset s_statusUpdatedAt = new(2026, 6, 24, 2, 3, 4, TimeSpan.Zero);
    private static readonly DateTimeOffset s_nextCheck = new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetRenewalState_WithDisabledCertificate_ReturnsDisabled()
    {
        var certificate = new CertificateRenewalTarget("example-com", Enabled: false, IsIssuedByAcmebot: true, IsSameEndpoint: true);
        var schedule = CreateSchedule("Scheduled");

        var state = CertificateRenewalStateEvaluator.GetRenewalState(certificate, schedule);

        Assert.Equal("Disabled", state.Status);
        Assert.Equal("disabled", state.StatusKind);
        Assert.Null(state.NextCheck);
        Assert.Equal(s_statusUpdatedAt, state.LastCheckedAt);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void GetRenewalState_WithCertificateNotManagedByThisEndpoint_ReturnsNotManaged(bool isIssuedByAcmebot, bool isSameEndpoint)
    {
        var certificate = new CertificateRenewalTarget("example-com", Enabled: true, isIssuedByAcmebot, isSameEndpoint);
        var schedule = CreateSchedule("Scheduled");

        var state = CertificateRenewalStateEvaluator.GetRenewalState(certificate, schedule);

        Assert.Equal("Not managed", state.Status);
        Assert.Equal("neutral", state.StatusKind);
        Assert.Null(state.NextCheck);
        Assert.Equal(s_statusUpdatedAt, state.LastCheckedAt);
    }

    [Fact]
    public void GetRenewalState_WithoutSchedule_ReturnsNotScheduled()
    {
        var state = CertificateRenewalStateEvaluator.GetRenewalState(CreateManagedCertificate(), schedule: null);

        Assert.Equal("Not scheduled", state.Status);
        Assert.Equal("pending", state.StatusKind);
        Assert.Null(state.NextCheck);
        Assert.Null(state.LastCheckedAt);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Failed)]
    [InlineData(OrchestrationRuntimeStatus.Terminated)]
    [InlineData(OrchestrationRuntimeStatus.Suspended)]
    public void GetRenewalState_WithStoppedOrchestration_ReturnsNeedsAttention(OrchestrationRuntimeStatus runtimeStatus)
    {
        var schedule = new CertificateRenewalScheduleSnapshot(null, runtimeStatus, "scheduler failed", s_lastUpdatedAt);

        var state = CertificateRenewalStateEvaluator.GetRenewalState(CreateManagedCertificate(), schedule);

        Assert.Equal("Needs attention", state.Status);
        Assert.Equal("attention", state.StatusKind);
        Assert.Equal("scheduler failed", state.Message);
        Assert.Null(state.NextCheck);
        Assert.Equal(s_lastUpdatedAt, state.LastCheckedAt);
    }

    [Theory]
    [InlineData("Scheduled", "scheduled", true)]
    [InlineData("Renewing", "active", false)]
    [InlineData("Retrying", "attention", true)]
    [InlineData("Stopped", "attention", false)]
    [InlineData("Checking", "pending", false)]
    public void GetRenewalState_WithKnownSchedulerStatus_ReturnsMappedState(string schedulerState, string expectedKind, bool expectsNextCheck)
    {
        var schedule = CreateSchedule(schedulerState);

        var state = CertificateRenewalStateEvaluator.GetRenewalState(CreateManagedCertificate(), schedule);

        Assert.Equal(schedulerState, state.Status);
        Assert.Equal(expectedKind, state.StatusKind);
        Assert.Equal($"{schedulerState} reason", state.Message);
        Assert.Equal(expectsNextCheck ? s_nextCheck : null, state.NextCheck);
        Assert.Equal(s_statusUpdatedAt, state.LastCheckedAt);
    }

    [Fact]
    public void GetRenewalState_WithUnknownSchedulerStatus_ReturnsChecking()
    {
        var schedule = CreateSchedule("Unexpected");

        var state = CertificateRenewalStateEvaluator.GetRenewalState(CreateManagedCertificate(), schedule);

        Assert.Equal("Checking", state.Status);
        Assert.Equal("pending", state.StatusKind);
        Assert.Equal("Automatic renewal status is being refreshed.", state.Message);
        Assert.Null(state.NextCheck);
        Assert.Equal(s_lastUpdatedAt, state.LastCheckedAt);
    }

    private static CertificateRenewalTarget CreateManagedCertificate() => new("example-com", Enabled: true, IsIssuedByAcmebot: true, IsSameEndpoint: true);

    private static CertificateRenewalScheduleSnapshot CreateSchedule(string state)
    {
        return new CertificateRenewalScheduleSnapshot(
            new CertificateRenewalSchedulerStatus
            {
                CertificateName = "example-com",
                State = state,
                NextCheck = s_nextCheck,
                Reason = $"{state} reason",
                UpdatedAt = s_statusUpdatedAt
            },
            OrchestrationRuntimeStatus.Running,
            null,
            s_lastUpdatedAt);
    }
}
