using Acmebot.App.Options;

using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

using Azure.Core;

namespace Acmebot.App.Providers;

public class Route53Provider(Route53Options options, TokenCredential tokenCredential) : IDnsProvider
{
    private readonly AmazonRoute53Client _amazonRoute53Client = CreateClient(options, tokenCredential);

    public string Name => "Amazon Route 53";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<HostedZone>();

        ListHostedZonesResponse? response = null;

        do
        {
            response = await _amazonRoute53Client.ListHostedZonesAsync(new ListHostedZonesRequest { Marker = response?.NextMarker }, cancellationToken);

            zones.AddRange(response.HostedZones);

        } while (response.IsTruncated ?? false);

        return zones.Where(x => x.Config?.PrivateZone != true)
                    .Select(x => new DnsZone(this) { Id = x.Id, Name = x.Name.TrimEnd('.') })
                    .ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
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

        return _amazonRoute53Client.ChangeResourceRecordSetsAsync(request, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}.";

        var listRequest = new ListResourceRecordSetsRequest(zone.Id)
        {
            StartRecordName = recordName,
            StartRecordType = RRType.TXT
        };

        var listResponse = await _amazonRoute53Client.ListResourceRecordSetsAsync(listRequest, cancellationToken);

        var changes = listResponse.ResourceRecordSets
                                  .Where(x => x.Name == recordName && x.Type == RRType.TXT)
                                  .Select(x => new Change { Action = ChangeAction.DELETE, ResourceRecordSet = x })
                                  .ToList();

        if (changes.Count == 0)
        {
            return;
        }

        var request = new ChangeResourceRecordSetsRequest(zone.Id, new ChangeBatch(changes));

        await _amazonRoute53Client.ChangeResourceRecordSetsAsync(request, cancellationToken);
    }

    private static AmazonRoute53Client CreateClient(Route53Options options, TokenCredential tokenCredential)
    {
        if (!string.IsNullOrWhiteSpace(options.RoleArn))
        {
            return new AmazonRoute53Client(new ManagedIdentityWebIdentityCredentials(options.RoleArn, tokenCredential), RegionEndpoint.USEast1);
        }

        return new AmazonRoute53Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), RegionEndpoint.USEast1);
    }

    private sealed class ManagedIdentityWebIdentityCredentials(string roleArn, TokenCredential tokenCredential) : RefreshingAWSCredentials
    {
        private const string Audience = "https://management.azure.com/";

        protected override async Task<CredentialsRefreshState> GenerateNewCredentialsAsync()
        {
            var token = await tokenCredential.GetTokenAsync(new TokenRequestContext([Audience]), CancellationToken.None);

            using var securityTokenServiceClient = new AmazonSecurityTokenServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1);

            var request = new AssumeRoleWithWebIdentityRequest
            {
                RoleArn = roleArn,
                RoleSessionName = "acmebot",
                WebIdentityToken = token.Token
            };

            var response = await securityTokenServiceClient.AssumeRoleWithWebIdentityAsync(request);

            var credentials = response.Credentials;

            var immutableCredentials = new ImmutableCredentials(credentials.AccessKeyId, credentials.SecretAccessKey, credentials.SessionToken);

            return new CredentialsRefreshState(immutableCredentials, credentials.Expiration ?? DateTime.MinValue);
        }
    }
}
