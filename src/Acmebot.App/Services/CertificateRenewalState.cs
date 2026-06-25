namespace Acmebot.App.Services;

internal sealed record CertificateRenewalState(
    string Status,
    string StatusKind,
    string Message,
    DateTimeOffset? NextCheck,
    DateTimeOffset? LastCheckedAt);
