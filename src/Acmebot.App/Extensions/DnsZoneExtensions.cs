using Acmebot.App.Models;
using Acmebot.App.Providers;

namespace Acmebot.App.Extensions;

internal static class DnsZoneExtensions
{
    public static DnsZoneItem ToDnsZoneItem(this DnsZone dnsZone) => new() { Name = dnsZone.Name };

    public static DnsZone? FindDnsZone(this IEnumerable<DnsZone> dnsZones, string dnsName)
    {
        return dnsZones.Where(x => string.Equals(dnsName, x.Name, StringComparison.OrdinalIgnoreCase) || dnsName.EndsWith($".{x.Name}", StringComparison.OrdinalIgnoreCase))
                       .MaxBy(x => x.Name.Length);
    }
}
