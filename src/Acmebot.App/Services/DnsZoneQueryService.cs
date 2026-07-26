using Acmebot.App.Extensions;
using Acmebot.App.Models;
using Acmebot.App.Providers;

using Microsoft.Extensions.Logging;

namespace Acmebot.App.Services;

public partial class DnsZoneQueryService(IEnumerable<IDnsProvider> dnsProviders, ILogger<DnsZoneQueryService> logger)
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
                catch (Exception ex)
                {
                    // A provider outage must not hide the zones of the other providers, but the failure
                    // still has to be recorded. Otherwise an expired credential is indistinguishable from
                    // a provider that genuinely hosts no zones.
                    LogDnsZoneListingFailed(logger, ex, dnsProvider.Name);

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
        catch (Exception ex)
        {
            LogDnsZoneEnumerationFailed(logger, ex);

            return [];
        }
    }

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(string dnsProviderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dnsProviderName);

        // Returning an empty zone list for an unknown provider would surface downstream as a misleading
        // "no DNS zone was found for <domain>" error, which sends operators looking at their DNS zones
        // instead of at the provider configuration that was renamed or removed.
        var dnsProvider = dnsProviders.FirstOrDefault(x => x.Name == dnsProviderName)
            ?? throw new PreconditionException($"The DNS provider '{dnsProviderName}' is not configured. Configured DNS providers: {FormatConfiguredProviderNames()}.");

        return await dnsProvider.ListZonesAsync(cancellationToken);
    }

    private string FormatConfiguredProviderNames()
    {
        var names = dnsProviders.Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();

        return names.Length > 0 ? string.Join(", ", names) : "(none)";
    }

    [LoggerMessage(LogLevel.Warning, "Listing DNS zones failed and the provider was reported as having no zones. DnsProviderName: {DnsProviderName}")]
    private static partial void LogDnsZoneListingFailed(ILogger logger, Exception exception, string dnsProviderName);

    [LoggerMessage(LogLevel.Error, "Enumerating the configured DNS providers failed. No DNS zones were returned.")]
    private static partial void LogDnsZoneEnumerationFailed(ILogger logger, Exception exception);
}
