using System.Text;

using Acmebot.App.Options;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.Apis.Json;
using Google.Apis.Services;

namespace Acmebot.App.Providers;

public class GoogleDnsProvider(GoogleDnsOptions options) : IDnsProvider
{
    private readonly DnsService _dnsService = new(new BaseClientService.Initializer
    {
        HttpClientInitializer = CredentialFactory.FromJson<GoogleCredential>(Encoding.UTF8.GetString(Convert.FromBase64String(options.KeyFile64)))
                                                 .CreateScoped(DnsService.Scope.NdevClouddnsReadwrite)
    });

    private readonly JsonCredentialParameters _credsParameters =
        NewtonsoftJsonSerializer.Instance.Deserialize<JsonCredentialParameters>(Encoding.UTF8.GetString(Convert.FromBase64String(options.KeyFile64)));

    public string Name => "Google Cloud DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<ManagedZone>();

        ManagedZonesListResponse? response = null;

        do
        {
            var request = _dnsService.ManagedZones.List(_credsParameters.ProjectId);

            request.PageToken = response?.NextPageToken;

            response = await request.ExecuteAsync(cancellationToken);

            zones.AddRange(response.ManagedZones ?? []);

        } while (!string.IsNullOrEmpty(response.NextPageToken));

        return zones.Select(x => new DnsZone(this) { Id = x.Name, Name = x.DnsName.TrimEnd('.'), NameServers = x.NameServers?.ToArray() ?? [] })
                    .ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}.";

        var change = new Change
        {
            Additions =
            [
                new ResourceRecordSet
                {
                    Name = recordName,
                    Type = "TXT",
                    Ttl = 60,
                    Rrdatas = values
                }
            ]
        };

        return _dnsService.Changes.Create(change, _credsParameters.ProjectId, zone.Id).ExecuteAsync(cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}.";

        var request = _dnsService.ResourceRecordSets.List(_credsParameters.ProjectId, zone.Id);

        request.Name = recordName;
        request.Type = "TXT";

        var txtRecords = await request.ExecuteAsync(cancellationToken);

        if (txtRecords.Rrsets is null or { Count: 0 })
        {
            return;
        }

        var change = new Change { Deletions = txtRecords.Rrsets };

        await _dnsService.Changes.Create(change, _credsParameters.ProjectId, zone.Id).ExecuteAsync(cancellationToken);
    }
}
