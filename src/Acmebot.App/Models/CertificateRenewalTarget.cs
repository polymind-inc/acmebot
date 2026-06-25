namespace Acmebot.App.Models;

public sealed record CertificateRenewalTarget(string Name, bool Enabled, bool IsIssuedByAcmebot, bool IsSameEndpoint)
{
    public bool IsRenewable => Enabled && IsIssuedByAcmebot && IsSameEndpoint;
}
