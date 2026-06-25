namespace Acmebot.App.Services;

internal static class CertificateRenewalScheduleEvaluator
{
    public static bool ShouldRenew(
        DateTimeOffset? notBefore,
        DateTimeOffset? createdOn,
        DateTimeOffset? expiresOn,
        int renewBeforeExpiry,
        DateTimeOffset now)
    {
        if (expiresOn is not { } expires)
        {
            return false;
        }

        var effectiveNotBefore = notBefore ?? createdOn;

        if (expires <= now)
        {
            return true;
        }

        if (effectiveNotBefore is null || effectiveNotBefore.Value > expires)
        {
            return false;
        }

        var lifetime = expires - effectiveNotBefore.Value;
        var renewalThreshold = TimeSpan.FromTicks((long)(lifetime.Ticks * (renewBeforeExpiry / 100d)));
        var suggestedWindowStart = expires - renewalThreshold;

        return suggestedWindowStart <= now;
    }

    public static DateTimeOffset SelectNextCheck(
        DateTimeOffset now,
        DateTimeOffset suggestedWindowStart,
        DateTimeOffset suggestedWindowEnd,
        TimeSpan? retryAfter,
        TimeSpan renewalInfoCheckInterval,
        Func<long, long>? nextInt64 = null)
    {
        var window = suggestedWindowEnd - suggestedWindowStart;
        var randomOffsetTicks = nextInt64?.Invoke(window.Ticks) ?? Random.Shared.NextInt64(window.Ticks);
        var randomRenewalTime = suggestedWindowStart.AddTicks(randomOffsetTicks);
        var nextRenewalInfoCheck = now.Add(retryAfter ?? renewalInfoCheckInterval);

        return randomRenewalTime <= nextRenewalInfoCheck ? randomRenewalTime : nextRenewalInfoCheck;
    }
}
