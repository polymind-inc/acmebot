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

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var recordSet = new RecordSet
        {
            Name = recordName,
            Type = "TXT",
            Ttl = 60,
            Rdata = values.ToArray()
        };

        await _akamaiEdgeDnsClient.CreateOrUpdateRecordAsync(zone.Name, recordName, "TXT", recordSet, cancellationToken);
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
            const int pageSize = 100;

            while (true)
            {
                var result = await _httpClient.GetFromJsonAsync<ZonesResponse>($"zones?page={page}&pageSize={pageSize}", cancellationToken);

                if (result?.Zones is null or { Length: 0 })
                {
                    break;
                }

                foreach (var zone in result.Zones)
                {
                    yield return zone;
                }

                page++;
            }
        }

        public async Task CreateOrUpdateRecordAsync(string zone, string name, string type, RecordSet recordSet, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"zones/{zone}/names/{name}/types/{type}", recordSet, cancellationToken);

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
        [JsonPropertyName("metadata")]
        public Metadata? Metadata { get; set; }

        [JsonPropertyName("zones")]
        public ZoneResult[]? Zones { get; set; }
    }

    internal class Metadata
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalElements")]
        public int TotalElements { get; set; }
    }

    internal class ZoneResult
    {
        [JsonPropertyName("zone")]
        public required string Zone { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("contractId")]
        public string? ContractId { get; set; }

        [JsonPropertyName("activationState")]
        public string? ActivationState { get; set; }
    }

    internal class RecordSet
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("rdata")]
        public string[]? Rdata { get; set; }
    }
}
