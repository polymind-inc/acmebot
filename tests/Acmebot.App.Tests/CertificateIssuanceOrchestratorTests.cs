using Acmebot.Acme.Models;
using Acmebot.App.Acme;
using Acmebot.App.Functions.Orchestration;
using Acmebot.App.Models;
using Acmebot.App.Providers;

using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateIssuanceOrchestratorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssueCertificate_PreservesOrder_AndSkipsEmptyDnsWork(bool hasChallenges)
    {
        var context = new IssuanceContext(hasChallenges);

        await new CertificateIssuanceOrchestrator().IssueCertificate(context, CreatePolicy());

        Assert.Equal(nameof(DnsChallengeActivities.Dns01Precondition), context.Calls[0]);
        Assert.Equal(nameof(AcmeOrderActivities.Order), context.Calls[1]);
        Assert.Contains(nameof(AcmeOrderActivities.CheckIsReady), context.Calls);
        Assert.Contains(nameof(AcmeOrderActivities.FinalizeOrder), context.Calls);
        Assert.Equal(hasChallenges ? 1 : 0, context.TimerCount);
        Assert.Equal(hasChallenges, context.Calls.Contains(nameof(DnsChallengeActivities.CheckDnsChallenge)));
        Assert.Equal(hasChallenges, context.Calls.Contains(nameof(AcmeOrderActivities.AnswerChallenges)));
        Assert.Equal(hasChallenges, context.Calls.Contains(nameof(DnsChallengeActivities.CleanupDnsChallenge)));
    }

    [Fact]
    public async Task IssueCertificate_WithoutAuthorizationUrls_StillWaitsForOrderReadiness()
    {
        var context = new IssuanceContext(false) { NoAuthorizationUrls = true };

        await new CertificateIssuanceOrchestrator().IssueCertificate(context, CreatePolicy());

        Assert.DoesNotContain(nameof(DnsChallengeActivities.Dns01Authorization), context.Calls);
        Assert.Contains(nameof(AcmeOrderActivities.CheckIsReady), context.Calls);
        Assert.DoesNotContain(nameof(DnsChallengeActivities.CheckDnsChallenge), context.Calls);
        Assert.DoesNotContain(nameof(DnsChallengeActivities.CleanupDnsChallenge), context.Calls);
        Assert.Equal(0, context.TimerCount);
    }

    [Fact]
    public async Task IssueCertificate_WhenChallengeCheckFails_CleansUpUsingPreparedZones()
    {
        var context = new IssuanceContext(true) { FailActivity = nameof(DnsChallengeActivities.CheckDnsChallenge) };

        await Assert.ThrowsAsync<PreconditionException>(() => new CertificateIssuanceOrchestrator().IssueCertificate(context, CreatePolicy()));

        Assert.Contains(nameof(DnsChallengeActivities.CleanupDnsChallenge), context.Calls);
        Assert.Contains(nameof(WebhookActivities.SendFailedEvent), context.Calls);
        Assert.DoesNotContain(nameof(AcmeOrderActivities.FinalizeOrder), context.Calls);
    }

    [Fact]
    public async Task IssueCertificate_WhenPreconditionFails_DoesNotCreateOrder()
    {
        var context = new IssuanceContext(true) { FailActivity = nameof(DnsChallengeActivities.Dns01Precondition) };

        await Assert.ThrowsAsync<PreconditionException>(() => new CertificateIssuanceOrchestrator().IssueCertificate(context, CreatePolicy()));

        Assert.DoesNotContain(nameof(AcmeOrderActivities.Order), context.Calls);
        Assert.Contains(nameof(WebhookActivities.SendFailedEvent), context.Calls);
    }

    [Fact]
    public async Task IssueCertificate_WhenOrderIsReady_StillRunsPreconditionFirst()
    {
        var context = new IssuanceContext(false) { OrderReady = true };

        await new CertificateIssuanceOrchestrator().IssueCertificate(context, CreatePolicy());

        Assert.Equal(nameof(DnsChallengeActivities.Dns01Precondition), context.Calls[0]);
        Assert.Equal(nameof(AcmeOrderActivities.Order), context.Calls[1]);
        Assert.DoesNotContain(nameof(DnsChallengeActivities.Dns01Authorization), context.Calls);
        Assert.DoesNotContain(nameof(AcmeOrderActivities.CheckIsReady), context.Calls);
        Assert.Equal(0, context.TimerCount);
    }

    private static CertificatePolicyItem CreatePolicy() => new()
    {
        CertificateName = "certificate",
        DnsNames = ["example.com"],
        DnsProviderName = "Test DNS",
        KeyType = "RSA"
    };

    private sealed class IssuanceContext(bool hasChallenges) : TaskOrchestrationContext
    {
        private readonly IReadOnlyList<DnsZone> _zones = [new() { Id = "zone-1", Name = "example.com" }];
        private readonly IReadOnlyList<AcmeChallengeResult> _challenges = hasChallenges
            ? [new() { Url = new Uri("https://acme.example/challenge"), DnsRecordName = "_acme-challenge.example.com", DnsRecordValue = "value" }]
            : [];
        public string? FailActivity { get; init; }
        public bool OrderReady { get; init; }
        public bool NoAuthorizationUrls { get; init; }
        public List<string> Calls { get; } = [];
        public int TimerCount { get; private set; }
        public override TaskName Name => nameof(CertificateIssuanceOrchestrator.IssueCertificate);
        public override string InstanceId => "issuance-test";
        public override ParentOrchestrationInstance? Parent => null;
        public override DateTime CurrentUtcDateTime => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public override bool IsReplaying => false;
        protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;
        public override T GetInput<T>() => throw new NotSupportedException();

        public override Task<TResult> CallActivityAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null)
        {
            Calls.Add(name.Name);
            if (name.Name == FailActivity)
            {
                return Task.FromException<TResult>(new PreconditionException("Test failure."));
            }

            object? result = null;
            switch (name.Name)
            {
                case nameof(DnsChallengeActivities.Dns01Precondition):
                    result = _zones;
                    break;
                case nameof(AcmeOrderActivities.Order):
                    result = Order(OrderReady ? AcmeOrderStatuses.Ready : AcmeOrderStatuses.Pending);
                    break;
                case nameof(DnsChallengeActivities.Dns01Authorization):
                    var authorization = Assert.IsType<(string, string?, IReadOnlyList<Uri>, IReadOnlyList<DnsZone>)>(input);
                    Assert.Equal("Test DNS", authorization.Item1);
                    Assert.Same(_zones, authorization.Item4);
                    result = (_challenges, hasChallenges ? 60 : 0);
                    break;
                case nameof(DnsChallengeActivities.CleanupDnsChallenge):
                    var cleanup = Assert.IsType<(string, IReadOnlyList<AcmeChallengeResult>, IReadOnlyList<DnsZone>)>(input);
                    Assert.Equal("Test DNS", cleanup.Item1);
                    Assert.Same(_challenges, cleanup.Item2);
                    Assert.Same(_zones, cleanup.Item3);
                    break;
                case nameof(AcmeOrderActivities.FinalizeOrder):
                    result = Order(AcmeOrderStatuses.Valid);
                    break;
                case nameof(AcmeOrderActivities.MergeCertificate):
                    result = new CertificateItem { Id = new Uri("https://vault.example/certificates/certificate"), Name = "certificate", DnsNames = ["example.com"], DnsProviderName = "Test DNS" };
                    break;
                case nameof(DnsChallengeActivities.CheckDnsChallenge):
                case nameof(AcmeOrderActivities.AnswerChallenges):
                case nameof(AcmeOrderActivities.CheckIsReady):
                case nameof(WebhookActivities.SendCompletedEvent):
                case nameof(WebhookActivities.SendFailedEvent):
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected activity: {name}.");
            }

            return Task.FromResult(result is null ? default! : (TResult)result);
        }

        private OrderDetails Order(AcmeOrderStatus status) => new()
        {
            OrderUrl = new Uri("https://acme.example/order"),
            Payload = new AcmeOrderResource { Status = status, Authorizations = NoAuthorizationUrls ? [] : [new Uri("https://acme.example/authorization")] }
        };

        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        {
            TimerCount++;
            Assert.Equal(CurrentUtcDateTime.AddSeconds(60), fireAt);
            return Task.CompletedTask;
        }

        public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override void SendEvent(string instanceId, string eventName, object? eventData) => throw new NotSupportedException();
        public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();
        public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null) => throw new NotSupportedException();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => throw new NotSupportedException();
        public override Guid NewGuid() => throw new NotSupportedException();
    }
}
