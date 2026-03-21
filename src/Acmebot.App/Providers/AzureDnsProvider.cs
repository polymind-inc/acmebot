using System.Net;

using Acmebot.App.Infrastructure;
using Acmebot.App.Options;

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;

namespace Acmebot.App.Providers;

public class AzureDnsProvider(AzureDnsOptions options, AzureEnvironment environment, TokenCredential credential) : IDnsProvider
{
    private readonly ArmClient _armClient = new(credential, options.SubscriptionId, new ArmClientOptions { Environment = environment.ResourceManager });

    public string Name => "Azure DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);

        await foreach (var zone in subscription.GetDnsZonesAsync(cancellationToken: cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id.ToString(), Name = zone.Data.Name, NameServers = zone.Data.NameServers });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        // TXT レコードに値をセットする
        var txtRecordData = new DnsTxtRecordData
        {
            TtlInSeconds = 60
        };

        foreach (var value in values)
        {
            txtRecordData.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { value } });
        }

        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));

        var dnsTxtRecords = dnsZoneResource.GetDnsTxtRecords();

        return dnsTxtRecords.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, txtRecordData, cancellationToken: cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var dnsZoneResource = _armClient.GetDnsZoneResource(new ResourceIdentifier(zone.Id));

        try
        {
            var dnsTxtRecordResource = await dnsZoneResource.GetDnsTxtRecordAsync(relativeRecordName, cancellationToken);

            await dnsTxtRecordResource.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }
    }
}
