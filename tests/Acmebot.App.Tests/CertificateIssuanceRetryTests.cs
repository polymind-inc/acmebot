using Acmebot.App.Functions.Orchestration;

using DurableTask.Core;
using DurableTask.Core.Exceptions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateIssuanceRetryTests
{
    [Fact]
    public async Task Polling_WhenReadyAfterOneMinute_CompletesSuccessfully()
    {
        var context = new TimerContext();
        var attempts = 0;
        var retry = CreateRetry(context, () =>
        {
            attempts++;
            return context.Elapsed < TimeSpan.FromMinutes(2)
                ? Task.FromException<bool>(CreateFailure(new RetriableActivityException("Still pending.")))
                : Task.FromResult(true);
        });

        Assert.True(await retry.Invoke());
        Assert.Equal(6, attempts);
        Assert.Equal(TimeSpan.FromSeconds(135), context.Elapsed);
        Assert.Equal([5, 10, 20, 40, 60], context.Delays.Select(x => x.TotalSeconds));
    }

    [Fact]
    public async Task Polling_WhenStillPending_StopsAfterTwelveAttempts()
    {
        var context = new TimerContext();
        var attempts = 0;
        var failure = CreateFailure(new RetriableActivityException("Still pending."));
        var retry = CreateRetry(context, () =>
        {
            attempts++;
            return Task.FromException<bool>(failure);
        });

        Assert.Same(failure, await Assert.ThrowsAsync<TaskFailedException>(() => retry.Invoke()));
        Assert.Equal(12, attempts);
        Assert.Equal(TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(15), context.Elapsed);
        Assert.All(context.Delays, delay => Assert.InRange(delay, TimeSpan.Zero, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Polling_WhenOrderIsInvalid_FailsWithoutRetrying()
    {
        var context = new TimerContext();
        var attempts = 0;
        var failure = CreateFailure(new InvalidOperationException("Order is invalid."));
        var retry = CreateRetry(context, () =>
        {
            attempts++;
            return Task.FromException<bool>(failure);
        });

        Assert.Same(failure, await Assert.ThrowsAsync<TaskFailedException>(() => retry.Invoke()));
        Assert.Equal(1, attempts);
        Assert.Empty(context.Delays);
    }

    private static TaskFailedException CreateFailure(Exception exception) => new("Activity failed.")
    {
        FailureDetails = new FailureDetails(exception)
    };

    private static RetryInterceptor<bool> CreateRetry(TimerContext context, Func<Task<bool>> activity)
    {
        // Exercise the Durable Task retry implementation with the same policy used by issuance.
        var policy = CertificateIssuanceOrchestrator.PollingRetryPolicy;
        var options = new RetryOptions(policy.FirstRetryInterval, policy.MaxNumberOfAttempts)
        {
            BackoffCoefficient = policy.BackoffCoefficient,
            MaxRetryInterval = policy.MaxRetryInterval,
            Handle = policy.Handle
        };

        return new RetryInterceptor<bool>(context, options, activity);
    }

    private sealed class TimerContext : OrchestrationContext
    {
        private static readonly DateTime s_startTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public List<TimeSpan> Delays { get; } = [];

        public TimeSpan Elapsed { get; private set; }

        public override DateTime CurrentUtcDateTime => s_startTime + Elapsed;

        public override Task<T> CreateTimer<T>(DateTime fireAt, T state)
        {
            Delays.Add(fireAt - CurrentUtcDateTime);
            Elapsed = fireAt - s_startTime;
            return Task.FromResult(state);
        }

        public override Task<T> CreateTimer<T>(DateTime fireAt, T state, CancellationToken cancelToken) => CreateTimer(fireAt, state);

        public override Task<TResult> ScheduleTask<TResult>(string name, string version, params object[] parameters) => throw new NotSupportedException();

        public override Task<T> CreateSubOrchestrationInstance<T>(string name, string version, object input) => throw new NotSupportedException();

        public override Task<T> CreateSubOrchestrationInstance<T>(string name, string version, string instanceId, object input) => throw new NotSupportedException();

        public override Task<T> CreateSubOrchestrationInstance<T>(string name, string version, string instanceId, object input, IDictionary<string, string> tags) => throw new NotSupportedException();

        public override void SendEvent(OrchestrationInstance orchestrationInstance, string eventName, object eventData) => throw new NotSupportedException();

        public override void ContinueAsNew(object input) => throw new NotSupportedException();

        public override void ContinueAsNew(string newVersion, object input) => throw new NotSupportedException();
    }
}
