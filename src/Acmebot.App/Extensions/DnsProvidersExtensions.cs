using Acmebot.App.Providers;

namespace Acmebot.App.Extensions;

internal static class DnsProvidersExtensions
{
    public static async Task<IReadOnlyList<(string, IReadOnlyList<DnsZone>)>> ListAllZonesAsync(this IEnumerable<IDnsProvider> dnsProviders)
    {
        async Task<(string, IReadOnlyList<DnsZone>)> ListDnsZones(IDnsProvider dnsProvider)
        {
            try
            {
                var dnsZones = await dnsProvider.ListZonesAsync();

                return (dnsProvider.Name, dnsZones);
            }
            catch
            {
                return (dnsProvider.Name, null);
            }
        }

        var zones = await Task.WhenAll(dnsProviders.Select(ListDnsZones));

        return zones;
    }

    public static async Task<IReadOnlyList<DnsZone>> ListZonesAsync(this IEnumerable<IDnsProvider> dnsProviders, string dnsProviderName)
    {
        var dnsProvider = dnsProviders.FirstOrDefault(x => x.Name == dnsProviderName);

        if (dnsProvider is null)
        {
            return [];
        }

        var dnsZones = await dnsProvider.ListZonesAsync();

        return dnsZones;
    }

    public static async Task<IReadOnlyList<DnsZone>> FlattenAllZonesAsync(this IEnumerable<IDnsProvider> dnsProviders)
    {
        var zones = await dnsProviders.ListAllZonesAsync();

        return zones.Where(x => x.Item2 is not null).SelectMany(x => x.Item2).ToArray();
    }

    public static void TryAdd<TOption>(this IList<IDnsProvider> dnsProviders, TOption? options, Func<TOption, IDnsProvider> factory)
    {
        if (options is not null)
        {
            dnsProviders.Add(factory(options));
        }
    }
}
