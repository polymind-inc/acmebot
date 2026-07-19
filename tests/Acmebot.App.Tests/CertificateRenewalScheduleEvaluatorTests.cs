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
    public void SelectRenewalTime_ReturnsTimeWithinSuggestedWindow()
    {
        var suggestedWindowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suggestedWindowEnd = suggestedWindowStart.AddDays(2);

        var result = CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE", suggestedWindowStart, suggestedWindowEnd);

        Assert.InRange(result, suggestedWindowStart, suggestedWindowEnd.AddTicks(-1));
    }

    [Fact]
    public void SelectRenewalTime_IsDeterministicForSameCertificateAndWindow()
    {
        var suggestedWindowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suggestedWindowEnd = suggestedWindowStart.AddDays(2);

        var first = CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE", suggestedWindowStart, suggestedWindowEnd);
        var second = CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE", suggestedWindowStart, suggestedWindowEnd);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectRenewalTime_VariesByCertificateIdentifier()
    {
        var suggestedWindowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var suggestedWindowEnd = suggestedWindowStart.AddDays(2);

        var first = CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE", suggestedWindowStart, suggestedWindowEnd);
        var second = CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AQAB", suggestedWindowStart, suggestedWindowEnd);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SelectRenewalTime_ThrowsForInvalidWindow()
    {
        var suggestedWindowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() => CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE", suggestedWindowStart, suggestedWindowStart));
        Assert.Throws<ArgumentException>(() => CertificateRenewalScheduleEvaluator.SelectRenewalTime("aYhba4dGQEHhs3uEe6CuLN4ByNQ.AIdlQyE", suggestedWindowStart, suggestedWindowStart.AddHours(-1)));
    }

    [Fact]
    public void SelectNextCheck_WhenRenewalTimeIsBeforeRetryCheck_ReturnsRenewalTime()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renewalTime = now.AddHours(2);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            renewalTime,
            retryAfter: TimeSpan.FromHours(4),
            renewalInfoCheckInterval: TimeSpan.FromHours(6));

        Assert.Equal(renewalTime, result);
    }

    [Fact]
    public void SelectNextCheck_WhenRetryCheckIsBeforeRenewalTime_ReturnsRetryCheck()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renewalTime = now.AddHours(3);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            renewalTime,
            retryAfter: TimeSpan.FromMinutes(30),
            renewalInfoCheckInterval: TimeSpan.FromHours(6));

        Assert.Equal(now.AddMinutes(30), result);
    }

    [Fact]
    public void SelectNextCheck_WithoutRetryAfter_UsesDefaultRenewalInfoCheckInterval()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renewalTime = now.AddHours(3);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            renewalTime,
            retryAfter: null,
            renewalInfoCheckInterval: TimeSpan.FromHours(1));

        Assert.Equal(now.AddHours(1), result);
    }

    [Fact]
    public void SelectNextCheck_WhenRenewalTimeIsInPast_ReturnsRetryCheck()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renewalTime = now.AddHours(-1);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            renewalTime,
            retryAfter: null,
            renewalInfoCheckInterval: TimeSpan.FromHours(6));

        Assert.Equal(now.AddHours(6), result);
    }

    [Fact]
    public void SelectNextCheck_ClampsRetryAfterBelowOneMinute()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renewalTime = now.AddDays(3);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            renewalTime,
            retryAfter: TimeSpan.Zero,
            renewalInfoCheckInterval: TimeSpan.FromHours(6));

        Assert.Equal(now.AddMinutes(1), result);
    }

    [Fact]
    public void SelectNextCheck_ClampsRetryAfterAboveOneDay()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var renewalTime = now.AddDays(30);

        var result = CertificateRenewalScheduleEvaluator.SelectNextCheck(
            now,
            renewalTime,
            retryAfter: TimeSpan.FromDays(14),
            renewalInfoCheckInterval: TimeSpan.FromHours(6));

        Assert.Equal(now.AddDays(1), result);
    }
}
