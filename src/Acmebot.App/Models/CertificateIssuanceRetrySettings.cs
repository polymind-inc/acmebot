namespace Acmebot.App.Models;

public sealed record CertificateIssuanceRetrySettings
{
    public required int DnsChallengeCheckMaxAttempts { get; init; }

    public required int DnsChallengeCheckIntervalSeconds { get; init; }

    public required int OrderPollingMaxAttempts { get; init; }

    public required int OrderPollingIntervalSeconds { get; init; }
}
