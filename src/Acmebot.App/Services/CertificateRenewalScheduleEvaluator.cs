using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Acmebot.App.Services;

internal static class CertificateRenewalScheduleEvaluator
{
    // RFC 9773 Section 4.3.2 requires reasonable limits on the renewalInfo checking interval.
    // The bounds follow the example given in the RFC: under one minute is treated as one
    // minute, over one day is treated as one day.
    private static readonly TimeSpan s_minCheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_maxCheckInterval = TimeSpan.FromDays(1);

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

    public static DateTimeOffset SelectRenewalTime(string certificateIdentifier, DateTimeOffset suggestedWindowStart, DateTimeOffset suggestedWindowEnd)
    {
        if (suggestedWindowEnd <= suggestedWindowStart)
        {
            throw new ArgumentException("The suggested renewal window end must be after its start.", nameof(suggestedWindowEnd));
        }

        // RFC 9773 Section 4.2 requires selecting a uniform random time within the suggested
        // window and renewing at exactly that moment. Deriving the offset from a hash of the
        // certificate identifier and the window keeps the selection stable across evaluations
        // without persisting state, while still spreading renewals across clients.
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes($"{certificateIdentifier}|{suggestedWindowStart.UtcTicks}|{suggestedWindowEnd.UtcTicks}"));
        var windowTicks = (suggestedWindowEnd - suggestedWindowStart).Ticks;
        var offsetTicks = (long)(BinaryPrimitives.ReadUInt64LittleEndian(seed) % (ulong)windowTicks);

        return suggestedWindowStart.AddTicks(offsetTicks);
    }

    public static DateTimeOffset SelectNextCheck(
        DateTimeOffset now,
        DateTimeOffset renewalTime,
        TimeSpan? retryAfter,
        TimeSpan renewalInfoCheckInterval)
    {
        var interval = ClampCheckInterval(retryAfter ?? renewalInfoCheckInterval);
        var nextRenewalInfoCheck = now.Add(interval);

        return renewalTime > now && renewalTime < nextRenewalInfoCheck ? renewalTime : nextRenewalInfoCheck;
    }

    private static TimeSpan ClampCheckInterval(TimeSpan interval)
    {
        if (interval < s_minCheckInterval)
        {
            return s_minCheckInterval;
        }

        return interval > s_maxCheckInterval ? s_maxCheckInterval : interval;
    }
}
