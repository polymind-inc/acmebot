using System.Text;

using Acmebot.App.Options;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.Apis.Services;

namespace Acmebot.App.Providers;

public class GoogleDnsProvider : IDnsProvider
{
    public GoogleDnsProvider(GoogleDnsOptions options)
    {
        var serviceAccount = CredentialFactory.FromJson<ServiceAccountCredential>(Encoding.UTF8.GetString(Convert.FromBase64String(options.KeyFile64)));

        _projectId = serviceAccount.ProjectId;
        _credential = serviceAccount.ToGoogleCredential().CreateScoped(DnsService.Scope.NdevClouddnsReadwrite);

        _dnsService = new DnsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = _credential
        });
    }

    private readonly string _projectId;
    private readonly GoogleCredential _credential;
    private readonly DnsService _dnsService;

    public string Name => "Google Cloud DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<ManagedZone>();

        ManagedZonesListResponse? response = null;

        do
        {
            var request = _dnsService.ManagedZones.List(_projectId);

            request.PageToken = response?.NextPageToken;

            response = await request.ExecuteAsync(cancellationToken);

            zones.AddRange(response.ManagedZones ?? []);

        } while (!string.IsNullOrEmpty(response.NextPageToken));

        return zones.Where(x => !string.Equals(x.Visibility, "private", StringComparison.OrdinalIgnoreCase))
                    .Select(x => new DnsZone(this) { Id = x.Name, Name = x.DnsName.TrimEnd('.'), NameServers = x.NameServers?.ToArray() ?? [] })
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

        return _dnsService.Changes.Create(change, _projectId, zone.Id).ExecuteAsync(cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}.";

        var request = _dnsService.ResourceRecordSets.List(_projectId, zone.Id);

        request.Name = recordName;
        request.Type = "TXT";

        var txtRecords = await request.ExecuteAsync(cancellationToken);

        if (txtRecords.Rrsets is null or { Count: 0 })
        {
            return;
        }

        var change = new Change { Deletions = txtRecords.Rrsets };

        await _dnsService.Changes.Create(change, _projectId, zone.Id).ExecuteAsync(cancellationToken);
    }
}
