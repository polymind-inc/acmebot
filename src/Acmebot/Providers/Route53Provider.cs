using Acmebot.Options;

using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;

namespace Acmebot.Providers;

public class Route53Provider : IDnsProvider
{
    public Route53Provider(Route53Options options)
    {
        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);

        _amazonRoute53Client = new AmazonRoute53Client(credentials, RegionEndpoint.GetBySystemName(options.Region));
    }

    private readonly AmazonRoute53Client _amazonRoute53Client;

    public string Name => "Amazon Route 53";

    public int PropagationSeconds => 10;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = new List<HostedZone>();

        ListHostedZonesResponse? response = null;

        do
        {
            response = await _amazonRoute53Client.ListHostedZonesAsync(new ListHostedZonesRequest
            {
                Marker = response?.NextMarker
            });

            zones.AddRange(response.HostedZones);

        } while (response.IsTruncated ?? false);

        return zones.Select(x => new DnsZone(this) { Id = x.Id, Name = x.Name.TrimEnd('.') }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}.";

        var change = new Change
        {
            Action = ChangeAction.CREATE,
            ResourceRecordSet = new ResourceRecordSet
            {
                Name = recordName,
                Type = RRType.TXT,
                TTL = 60,
                ResourceRecords = values.Select(x => new ResourceRecord($"\"{x}\"")).ToList()
            }
        };

        var request = new ChangeResourceRecordSetsRequest(zone.Id, new ChangeBatch([change]));

        return _amazonRoute53Client.ChangeResourceRecordSetsAsync(request);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}.";

        var listRequest = new ListResourceRecordSetsRequest(zone.Id)
        {
            StartRecordName = recordName,
            StartRecordType = RRType.TXT
        };

        var listResponse = await _amazonRoute53Client.ListResourceRecordSetsAsync(listRequest);

        var changes = listResponse.ResourceRecordSets
                                  .Where(x => x.Name == recordName && x.Type == RRType.TXT)
                                  .Select(x => new Change { Action = ChangeAction.DELETE, ResourceRecordSet = x })
                                  .ToList();

        if (changes.Count == 0)
        {
            return;
        }

        var request = new ChangeResourceRecordSetsRequest(zone.Id, new ChangeBatch(changes));

        await _amazonRoute53Client.ChangeResourceRecordSetsAsync(request);
    }
}
