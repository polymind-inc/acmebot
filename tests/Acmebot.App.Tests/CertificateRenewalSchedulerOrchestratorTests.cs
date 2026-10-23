using System.Text.Json;

using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;

using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateRenewalSchedulerOrchestratorTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduleCertificateRenewal_WithPolicySnapshot_SkipsPolicyRetrieval(bool replaying)
    {
        var evaluation = ReadLegacyEvaluation() with { CertificatePolicy = CreatePolicy("snapshot.example.com") };
        evaluation = JsonSerializer.Deserialize<CertificateRenewalEvaluation>(JsonSerializer.Serialize(evaluation, s_jsonOptions), s_jsonOptions)!;
        var context = new RenewalContext(evaluation)
        {
            Replaying = replaying,
            FailCall = nameof(CertificateActivities.GetCertificatePolicy)
        };

        await new CertificateRenewalSchedulerOrchestrator().ScheduleCertificateRenewal(context, "example-com");

        Assert.Equal(new[] { nameof(CertificateActivities.EvaluateCertificateRenewal), nameof(CertificateIssuanceOrchestrator.IssueCertificate) }, context.Calls);
        Assert.Same(evaluation.CertificatePolicy, context.IssuedPolicy);
        Assert.Equal(new[] { "snapshot.example.com" }, context.IssuedPolicy!.DnsNames);
        Assert.Equal("certificate-id", context.IssuedPolicy.CertificateId);
        Assert.Equal("production", context.IssuedPolicy.Tags!["environment"]);
        Assert.Equal(1, context.ContinueCount);
        Assert.Empty(context.Timers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduleCertificateRenewal_WithLegacyEvaluation_PreservesActivitySequence(bool replaying)
    {
        var evaluation = ReadLegacyEvaluation();
        Assert.Null(evaluation.CertificatePolicy);
        var context = new RenewalContext(evaluation) { Replaying = replaying };

        await new CertificateRenewalSchedulerOrchestrator().ScheduleCertificateRenewal(context, "example-com");

        Assert.Equal(new[]
        {
            nameof(CertificateActivities.EvaluateCertificateRenewal),
            nameof(CertificateActivities.GetCertificatePolicy),
            nameof(CertificateIssuanceOrchestrator.IssueCertificate)
        }, context.Calls);
        Assert.Same(context.CurrentPolicy, context.IssuedPolicy);
        Assert.Equal(1, context.ContinueCount);
        Assert.Empty(context.Timers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduleCertificateRenewal_WhenNotDueOrInactive_DoesNotRetrievePolicy(bool active)
    {
        var evaluation = ReadLegacyEvaluation() with { IsActive = active, ShouldRenew = false };
        var context = new RenewalContext(evaluation) { Replaying = true };

        await new CertificateRenewalSchedulerOrchestrator().ScheduleCertificateRenewal(context, "example-com");

        Assert.Equal(new[] { nameof(CertificateActivities.EvaluateCertificateRenewal) }, context.Calls);
        Assert.Null(context.IssuedPolicy);
        Assert.Equal(active ? 1 : 0, context.ContinueCount);
        if (active)
        {
            Assert.Equal(evaluation.NextCheck.UtcDateTime, Assert.Single(context.Timers));
            Assert.Equal("Scheduled", context.Statuses[^1].State);
        }
        else
        {
            Assert.Empty(context.Timers);
            Assert.Equal("Stopped", context.Statuses[^1].State);
        }
    }

    [Fact]
    public async Task ScheduleCertificateRenewal_WhenLegacyPolicyWasDeleted_Stops()
    {
        var context = new RenewalContext(ReadLegacyEvaluation())
        {
            Replaying = true,
            FailCall = nameof(CertificateActivities.GetCertificatePolicy),
            Failure = new TaskFailedException(nameof(CertificateActivities.GetCertificatePolicy), 1, new PreconditionException("Certificate was not found."))
        };

        await new CertificateRenewalSchedulerOrchestrator().ScheduleCertificateRenewal(context, "example-com");

        Assert.Equal(new[] { nameof(CertificateActivities.EvaluateCertificateRenewal), nameof(CertificateActivities.GetCertificatePolicy) }, context.Calls);
        Assert.Equal("Stopped", context.Statuses[^1].State);
        Assert.Equal("Certificate was not found.", context.Statuses[^1].Reason);
        Assert.Null(context.IssuedPolicy);
        Assert.Empty(context.Timers);
        Assert.Equal(0, context.ContinueCount);
    }

    [Theory]
    [InlineData(nameof(CertificateActivities.EvaluateCertificateRenewal))]
    [InlineData(nameof(CertificateActivities.GetCertificatePolicy))]
    public async Task ScheduleCertificateRenewal_WhenEvaluationOrLegacyRetrievalFails_Retries(string failedActivity)
    {
        var context = new RenewalContext(ReadLegacyEvaluation()) { Replaying = true, FailCall = failedActivity };

        await new CertificateRenewalSchedulerOrchestrator().ScheduleCertificateRenewal(context, "example-com");

        Assert.Null(context.IssuedPolicy);
        Assert.Equal("Retrying", context.Statuses[^1].State);
        Assert.Equal(context.CurrentUtcDateTime.AddHours(6), Assert.Single(context.Timers));
        Assert.Equal(1, context.ContinueCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduleCertificateRenewal_WhenIssuanceFails_RetriesWithBothEvaluationFormats(bool hasSnapshot)
    {
        var evaluation = ReadLegacyEvaluation() with { CertificatePolicy = hasSnapshot ? CreatePolicy("snapshot.example.com") : null };
        var context = new RenewalContext(evaluation) { Replaying = true, FailCall = nameof(CertificateIssuanceOrchestrator.IssueCertificate) };

        await new CertificateRenewalSchedulerOrchestrator().ScheduleCertificateRenewal(context, "example-com");

        Assert.Equal(hasSnapshot ? evaluation.CertificatePolicy : context.CurrentPolicy, context.IssuedPolicy);
        Assert.Equal("Retrying", context.Statuses[^1].State);
        Assert.Equal(context.CurrentUtcDateTime.AddHours(6), Assert.Single(context.Timers));
        Assert.Equal(1, context.ContinueCount);
    }

    private static CertificateRenewalEvaluation ReadLegacyEvaluation() => JsonSerializer.Deserialize<CertificateRenewalEvaluation>("""
        {
          "isActive": true,
          "shouldRenew": true,
          "nextCheck": "2026-10-24T00:00:00+00:00",
          "reason": "Renewal is due."
        }
        """, s_jsonOptions)!;

    private static CertificatePolicyItem CreatePolicy(string dnsName) => new()
    {
        CertificateName = "example-com",
        DnsNames = [dnsName],
        DnsProviderName = "Azure DNS",
        KeyType = "RSA",
        KeySize = 2048,
        CertificateId = "certificate-id",
        Tags = new Dictionary<string, string> { ["environment"] = "production" }
    };

    private sealed class RenewalContext(CertificateRenewalEvaluation evaluation) : TaskOrchestrationContext
    {
        public CertificatePolicyItem CurrentPolicy { get; } = CreatePolicy("latest.example.com");
        public CertificatePolicyItem? IssuedPolicy { get; private set; }
        public string? FailCall { get; init; }
        public Exception Failure { get; init; } = new InvalidOperationException("Test failure.");
        public bool Replaying { get; init; }
        public List<string> Calls { get; } = [];
        public List<DateTime> Timers { get; } = [];
        public List<CertificateRenewalSchedulerStatus> Statuses { get; } = [];
        public int ContinueCount { get; private set; }
        public override TaskName Name => nameof(CertificateRenewalSchedulerOrchestrator.ScheduleCertificateRenewal);
        public override string InstanceId => "renewal-test";
        public override ParentOrchestrationInstance? Parent => null;
        public override DateTime CurrentUtcDateTime => new(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);
        public override bool IsReplaying => Replaying;
        protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
        public override T GetInput<T>() => throw new NotSupportedException();

        public override Task<TResult> CallActivityAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            Calls.Add(name.Name);
            Assert.Equal("example-com", input);
            if (name.Name == FailCall)
            {
                return Task.FromException<TResult>(Failure);
            }

            object result = name.Name switch
            {
                nameof(CertificateActivities.EvaluateCertificateRenewal) => evaluation,
                nameof(CertificateActivities.GetCertificatePolicy) => CurrentPolicy,
                _ => throw new InvalidOperationException($"Unexpected activity: {name}.")
            };
            return Task.FromResult((TResult)result);
        }

        public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            Calls.Add(name.Name);
            Assert.Equal(nameof(CertificateIssuanceOrchestrator.IssueCertificate), name.Name);
            IssuedPolicy = Assert.IsType<CertificatePolicyItem>(input);
            return name.Name == FailCall ? Task.FromException<TResult>(Failure) : Task.FromResult(default(TResult)!);
        }

        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        {
            Timers.Add(fireAt);
            return Task.CompletedTask;
        }

        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
        {
            Assert.Equal("example-com", newInput);
            ContinueCount++;
        }

        public override void SetCustomStatus(object? customStatus) => Statuses.Add(Assert.IsType<CertificateRenewalSchedulerStatus>(customStatus));
        public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void SendEvent(string instanceId, string eventName, object? eventData) => throw new NotSupportedException();
        public override Guid NewGuid() => throw new NotSupportedException();
    }
}
