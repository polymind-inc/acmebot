using Acmebot.App.Models;
using Acmebot.App.Providers;

namespace Acmebot.App.Extensions;

internal static class DnsZoneExtensions
{
    public static DnsZoneItem ToDnsZoneItem(this DnsZone dnsZone) => new() { Name = dnsZone.Name };

    public static DnsZone? FindDnsZone(this IEnumerable<DnsZone> dnsZones, string dnsName)
    {
        DnsZone? bestMatch = null;

        foreach (var dnsZone in dnsZones)
        {
            if (!IsInZone(dnsName, dnsZone.Name))
            {
                continue;
            }

            if (bestMatch is null || dnsZone.Name.Length > bestMatch.Name.Length)
            {
                bestMatch = dnsZone;
            }
        }

        return bestMatch;
    }

    private static bool IsInZone(string dnsName, string zoneName)
    {
        if (dnsName.Length < zoneName.Length)
        {
            return false;
        }

        if (!dnsName.EndsWith(zoneName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return dnsName.Length == zoneName.Length ||
               dnsName[dnsName.Length - zoneName.Length - 1] == '.';
    }
}
