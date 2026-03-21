using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

using Akamai.EdgeGrid.Auth;

namespace Acmebot.App.Providers;

public class AkamaiEdgeDnsProvider(AkamaiEdgeDnsOptions options) : IDnsProvider
{
    private readonly AkamaiEdgeDnsClient _akamaiEdgeDnsClient = new(options.Host, options.ClientToken, options.ClientSecret, options.AccessToken);

    public string Name => "Akamai Edge DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(120);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var zone in _akamaiEdgeDnsClient.ListZonesInternalAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Zone, Name = zone.Zone, NameServers = [] });
        }

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var recordSet = new RecordSet
        {
            Name = recordName,
            Type = "TXT",
            Ttl = 60,
            Rdata = values
        };

        await _akamaiEdgeDnsClient.CreateRecordAsync(zone.Name, recordSet, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        try
        {
            await _akamaiEdgeDnsClient.DeleteRecordAsync(zone.Name, recordName, "TXT", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignored
        }
    }

    private class AkamaiEdgeDnsClient
    {
        public AkamaiEdgeDnsClient(string host, string clientToken, string clientSecret, string accessToken)
        {
            _httpClient = EdgeGridSigner.CreateHttpClient(new EdgeGridCredentials(host, clientToken, clientSecret, accessToken));

            _httpClient.BaseAddress = new Uri($"https://{host}/config-dns/v2/");
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<ZoneResult> ListZonesInternalAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = 1;

            while (true)
            {
                var result = await _httpClient.GetFromJsonAsync<ZonesResponse>($"zones?page={page}&pageSize=100&types=PRIMARY", cancellationToken);

                if (result?.Zones is null or { Length: 0 })
                {
                    break;
                }

                foreach (var zone in result.Zones.Where(x => x.ActivationState == "ACTIVE"))
                {
                    yield return zone;
                }

                page++;
            }
        }

        public async Task CreateRecordAsync(string zone, RecordSet recordSet, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"zones/{zone}/names/{recordSet.Name}/types/{recordSet.Type}", recordSet, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zone, string name, string type, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zone}/names/{name}/types/{type}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class ZonesResponse
    {
        [JsonPropertyName("zones")]
        public ZoneResult[]? Zones { get; set; }
    }

    internal class ZoneResult
    {
        [JsonPropertyName("zone")]
        public required string Zone { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("activationState")]
        public string? ActivationState { get; set; }
    }

    internal class RecordSet
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("rdata")]
        public string[]? Rdata { get; set; }
    }
}
