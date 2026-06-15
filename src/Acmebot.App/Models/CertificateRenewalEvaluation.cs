namespace Acmebot.App.Models;

public sealed record CertificateRenewalEvaluation
{
    public required bool IsActive { get; init; }

    public required bool ShouldRenew { get; init; }

    public required DateTimeOffset NextCheck { get; init; }

    public required string Reason { get; init; }
}
