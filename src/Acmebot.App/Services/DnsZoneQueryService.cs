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
            var zones = await Task.WhenAll(dnsProviders.Select(async dnsProvider =>
            {
                try
                {
                    var dnsZones = await dnsProvider.ListZonesAsync(cancellationToken);

                    return new DnsZoneGroup
                    {
                        DnsProviderName = dnsProvider.Name,
                        DnsZones = dnsZones.Select(x => x.ToDnsZoneItem()).OrderBy(x => x.Name).ToArray()
                    };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    return new DnsZoneGroup
                    {
                        DnsProviderName = dnsProvider.Name,
                        DnsZones = []
                    };
                }
            }));

            return zones.ToArray();
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

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(string dnsProviderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dnsProviderName);

        var dnsProvider = dnsProviders.FirstOrDefault(x => x.Name == dnsProviderName);

        if (dnsProvider is null)
        {
            return [];
        }

        return await dnsProvider.ListZonesAsync(cancellationToken);
    }
}
