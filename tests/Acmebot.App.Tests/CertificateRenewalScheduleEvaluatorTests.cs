using Acmebot.App.Services;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class CertificateRenewalScheduleEvaluatorTests
{
    [Fact]
    public void ShouldRenew_WithoutExpirationDate_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore: now.AddDays(-10),
            createdOn: now.AddDays(-10),
            expiresOn: null,
            renewBeforeExpiry: 30,
            now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRenew_WithExpiredCertificate_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore: now.AddDays(-90),
            createdOn: null,
            expiresOn: now,
            renewBeforeExpiry: 30,
            now);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRenew_WithoutValidStartDate_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore: null,
            createdOn: null,
            expiresOn: now.AddDays(10),
            renewBeforeExpiry: 30,
            now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRenew_WithStartDateAfterExpiration_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore: now.AddDays(20),
            createdOn: null,
            expiresOn: now.AddDays(10),
            renewBeforeExpiry: 30,
            now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRenew_BeforeRenewalThreshold_ReturnsFalse()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresOn = notBefore.AddDays(10);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore,
            createdOn: null,
            expiresOn,
            renewBeforeExpiry: 30,
            now: expiresOn.AddDays(-3).AddTicks(-1));

        Assert.False(result);
    }

    [Fact]
    public void ShouldRenew_AtRenewalThreshold_ReturnsTrue()
    {
        var notBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresOn = notBefore.AddDays(10);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore,
            createdOn: null,
            expiresOn,
            renewBeforeExpiry: 30,
            now: expiresOn.AddDays(-3));

        Assert.True(result);
    }

    [Fact]
    public void ShouldRenew_UsesCreatedOnWhenNotBeforeIsMissing()
    {
        var createdOn = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiresOn = createdOn.AddDays(10);

        var result = CertificateRenewalScheduleEvaluator.ShouldRenew(
            notBefore: null,
            createdOn,
            expiresOn,
            renewBeforeExpiry: 30,
            now: expiresOn.AddDays(-3));

        Assert.True(result);
    }

    [Fact]
    public void SelectNextCheck_WhenRandomRenewalTimeIsBeforeRetryCheck_ReturnsRandomRenewalTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suggestedWindowStart = now.AddHours(1);
        var suggestedWindowEnd = now.AddHours(3);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            suggestedWindowStart,
            suggestedWindowEnd,
            retryAfter: TimeSpan.FromHours(4),
            renewalInfoCheckInterval: TimeSpan.FromHours(6),
            nextInt64: _ => TimeSpan.FromHours(1).Ticks);

        Assert.Equal(now.AddHours(2), result);
    }

    [Fact]
    public void SelectNextCheck_WhenRetryCheckIsBeforeRandomRenewalTime_ReturnsRetryCheck()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suggestedWindowStart = now.AddHours(2);
        var suggestedWindowEnd = now.AddHours(5);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            suggestedWindowStart,
            suggestedWindowEnd,
            retryAfter: TimeSpan.FromMinutes(30),
            renewalInfoCheckInterval: TimeSpan.FromHours(6),
            nextInt64: _ => TimeSpan.FromHours(1).Ticks);

        Assert.Equal(now.AddMinutes(30), result);
    }

    [Fact]
    public void SelectNextCheck_WithoutRetryAfter_UsesDefaultRenewalInfoCheckInterval()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suggestedWindowStart = now.AddHours(2);
        var suggestedWindowEnd = now.AddHours(5);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            suggestedWindowStart,
            suggestedWindowEnd,
            retryAfter: null,
            renewalInfoCheckInterval: TimeSpan.FromHours(1),
            nextInt64: _ => TimeSpan.FromHours(1).Ticks);

        Assert.Equal(now.AddHours(1), result);
    }
}
