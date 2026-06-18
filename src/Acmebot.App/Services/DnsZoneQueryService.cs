using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Providers;

namespace Acmebot.App.Services;

public class DnsZoneQueryService(IEnumerable<IDnsProvider> dnsProviders)
{
    public async Task<IReadOnlyList<DnsZoneGroup>> GetAllDnsZonesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var zones = await dnsProviders.ListAllZonesAsync(cancellationToken);

            return zones.Where(x => x.Item2 is not null)
                        .Select(x => new DnsZoneGroup
                        {
                            DnsProviderName = x.Item1,
                            DnsZones = x.Item2!.Select(xs => xs.ToDnsZoneItem()).OrderBy(xs => xs.Name).ToArray()
                        }).ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }
}
