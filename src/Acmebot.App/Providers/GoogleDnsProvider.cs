using System.Text;

using Acmebot.App.Options;

using Azure.Core;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.Apis.Services;

namespace Acmebot.App.Providers;

public class GoogleDnsProvider : IDnsProvider
{
    public GoogleDnsProvider(GoogleDnsOptions options, TokenCredential tokenCredential)
    {
        GoogleCredential? credential;

        if (!string.IsNullOrWhiteSpace(options.KeyFile64))
        {
            var serviceAccount = CredentialFactory.FromJson<ServiceAccountCredential>(Encoding.UTF8.GetString(Convert.FromBase64String(options.KeyFile64)));

            _projectId = string.IsNullOrWhiteSpace(options.ProjectId) ? serviceAccount.ProjectId : options.ProjectId;
            credential = serviceAccount.ToGoogleCredential();
        }
        else if (!string.IsNullOrWhiteSpace(options.PoolProvider) && !string.IsNullOrWhiteSpace(options.ServiceAccount) && !string.IsNullOrWhiteSpace(options.ProjectId))
        {
            var initializer = new ProgrammaticExternalAccountCredential.Initializer("https://sts.googleapis.com/v1/token",
                                                                                    $"//iam.googleapis.com/{options.PoolProvider}",
                                                                                    "urn:ietf:params:oauth:token-type:jwt",
                                                                                    new ManagedIdentitySubjectTokenProvider(tokenCredential))
            {
                ServiceAccountImpersonationUrl = $"https://iamcredentials.googleapis.com/v1/projects/-/serviceAccounts/{options.ServiceAccount}:generateAccessToken"
            };

            _projectId = options.ProjectId;
            credential = new ProgrammaticExternalAccountCredential(initializer).ToGoogleCredential();
        }
        else
        {
            throw new InvalidOperationException("Google Cloud DNS requires either KeyFile64 or all of ProjectId, PoolProvider, and ServiceAccount to be configured.");
        }

        _dnsService = new DnsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential.CreateScoped(DnsService.Scope.NdevClouddnsReadwrite)
        });
    }

    private readonly string _projectId;
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

    private sealed class ManagedIdentitySubjectTokenProvider(TokenCredential tokenCredential) : ProgrammaticExternalAccountCredential.ISubjectTokenProvider
    {
        private const string Audience = "https://management.azure.com/";

        public async Task<string> GetSubjectTokenAsync(ProgrammaticExternalAccountCredential caller, CancellationToken cancellationToken)
        {
            var token = await tokenCredential.GetTokenAsync(new TokenRequestContext([Audience]), cancellationToken);

            return token.Token;
        }
    }
}
