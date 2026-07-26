namespace Acmebot.App.Providers;

/// <summary>
/// Builds the fully qualified record name that a DNS provider API expects from the zone name and the
/// relative record name produced by the DNS-01 challenge. Zone names are reported inconsistently by the
/// provider APIs, so the trailing dot is normalized here instead of in each provider.
/// </summary>
internal static class DnsRecordName
{
    /// <summary>
    /// Builds the fully qualified record name without a trailing dot.
    /// </summary>
    public static string ToFqdn(string zoneName, string relativeRecordName) => $"{Normalize(relativeRecordName)}.{Normalize(zoneName)}";

    /// <summary>
    /// Builds the fully qualified record name in absolute form, with a trailing dot.
    /// </summary>
    public static string ToAbsoluteFqdn(string zoneName, string relativeRecordName) => $"{ToFqdn(zoneName, relativeRecordName)}.";

    private static string Normalize(string value) => value.Trim().TrimEnd('.');
}
