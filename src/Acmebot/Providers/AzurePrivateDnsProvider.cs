using System.Net;

using Acmebot.Internal;
using Acmebot.Options;

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.PrivateDns.Models;

namespace Acmebot.Providers;

internal class AzurePrivateDnsProvider(AzurePrivateDnsOptions options, AzureEnvironment environment, TokenCredential credential) : IDnsProvider
{
    private readonly ArmClient _armClient = new(credential, options.SubscriptionId, new ArmClientOptions { Environment = environment.ResourceManager });

    public string Name => "Azure Private DNS";

    public int PropagationSeconds => 10;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = new List<DnsZone>();

        var subscription = await _armClient.GetDefaultSubscriptionAsync();

        var result = subscription.GetPrivateDnsZonesAsync();

        await foreach (var zone in result)
        {
            zones.Add(new DnsZone(this) { Id = zone.Id.ToString(), Name = zone.Data.Name });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        // TXT レコードに値をセットする
        var txtRecordData = new PrivateDnsTxtRecordData() { TtlInSeconds = 3600 };

        foreach (var value in values)
        {
            txtRecordData.PrivateDnsTxtRecords.Add(new PrivateDnsTxtRecordInfo { Values = { value } });
        }

        var dnsZoneResource = _armClient.GetPrivateDnsZoneResource(new ResourceIdentifier(zone.Id));

        var dnsTxtRecords = dnsZoneResource.GetPrivateDnsTxtRecords();

        return dnsTxtRecords.CreateOrUpdateAsync(WaitUntil.Completed, relativeRecordName, txtRecordData);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var dnsZoneResource = _armClient.GetPrivateDnsZoneResource(new ResourceIdentifier(zone.Id));

        try
        {
            PrivateDnsTxtRecordResource dnsTxtRecordResource = await dnsZoneResource.GetPrivateDnsTxtRecordAsync(relativeRecordName);

            await dnsTxtRecordResource.DeleteAsync(WaitUntil.Completed);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            // ignored
        }
    }
}
