using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class CustomDnsProvider(CustomDnsOptions options) : IDnsProvider
{
    private readonly CustomDnsClient _customDnsClient = new(options.Endpoint, options.ApiKey, options.ApiKeyHeaderName);

    public string Name => "Custom DNS";

    public TimeSpan PropagationDelay { get; } = TimeSpan.FromSeconds(options.PropagationSeconds);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var zone in _customDnsClient.ListZonesAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Name, NameServers = zone.NameServers ?? [] });
        }

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var record = new RecordParam
        {
            Type = "TXT",
            Ttl = 60,
            Values = values
        };

        await _customDnsClient.CreateRecordAsync(zone.Id, recordName, record, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        try
        {
            await _customDnsClient.DeleteRecordAsync(zone.Id, recordName, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignored
        }
    }

    private class CustomDnsClient
    {
        public CustomDnsClient(string endpoint, string apiKey, string apiKeyHeaderName)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(apiKeyHeaderName, apiKey);
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<Zone> ListZonesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var zones = await _httpClient.GetFromJsonAsync<Zone[]>("zones", cancellationToken);

            if (zones is null or { Length: 0 })
            {
                yield break;
            }

            foreach (var zone in zones)
            {
                yield return zone;
            }
        }

        public async Task CreateRecordAsync(string zoneId, string recordName, RecordParam record, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"zones/{zoneId}/records/{recordName}", record, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneId, string recordName, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zoneId}/records/{recordName}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("nameServers")]
        public string[]? NameServers { get; set; }
    }

    internal class RecordParam
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("values")]
        public string[]? Values { get; set; }
    }
}
