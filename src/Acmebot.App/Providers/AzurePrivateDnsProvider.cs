using System.Net;

using Acmebot.App.Infrastructure;
using Acmebot.App.Options;

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.PrivateDns.Models;

namespace Acmebot.App.Providers;

public class AzurePrivateDnsProvider(AzurePrivateDnsOptions options, AzureEnvironment environment, TokenCredential credential) : IDnsProvider
{
    private readonly ArmClient _armClient = new(credential, options.SubscriptionId, new ArmClientOptions { Environment = environment.ResourceManager });

    public string Name => "Azure Private DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);

        await foreach (var zone in subscription.GetPrivateDnsZonesAsync(cancellationToken: cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id.ToString(), Name = zone.Data.Name });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        // TXT レコードに値をセットする
        var txtRecordData = new PrivateDnsTxtRecordData
        {
            TtlInSeconds = 3600
        };

        foreach (var value in values)
        {
            txtRecordData.PrivateDnsTxtRecords.Add(new PrivateDnsTxtRecordInfo { Values = { value } });
        }

        var dnsZoneResource = _armClient.GetPrivateDnsZoneResource(new ResourceIdentifier(zone.Id));

        var dnsTxtRecords = dnsZoneResource.GetPrivateDnsTxtRecords();

        return dnsTxtRecords.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, txtRecordData, cancellationToken: cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var dnsZoneResource = _armClient.GetPrivateDnsZoneResource(new ResourceIdentifier(zone.Id));

        try
        {
            var dnsTxtRecordResource = await dnsZoneResource.GetPrivateDnsTxtRecordAsync(relativeRecordName, cancellationToken);

            await dnsTxtRecordResource.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }
    }
}
