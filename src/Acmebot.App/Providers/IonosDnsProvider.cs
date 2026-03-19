using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class IonosDnsProvider(IonosDnsOptions options) : IDnsProvider
{
    private readonly IonosDnsClient _ionosDnsClient = new(options.ApiKey);

    public string Name => "IONOS DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(120);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var zone in _ionosDnsClient.ListZonesAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Name });
        }

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            await _ionosDnsClient.CreateRecordAsync(zone.Id, relativeRecordName, value, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var records = await _ionosDnsClient.ListRecordsAsync(zone.Id, relativeRecordName, cancellationToken);

        foreach (var record in records)
        {
            try
            {
                await _ionosDnsClient.DeleteRecordAsync(zone.Id, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class IonosDnsClient
    {
        public IonosDnsClient(string apiKey)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.hosting.ionos.com/dns/v1/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("X-API-Key", apiKey);
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<Zone> ListZonesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<Zone[]>("zones", cancellationToken);

            if (result is null or { Length: 0 })
            {
                yield break;
            }

            foreach (var zone in result)
            {
                yield return zone;
            }
        }

        public async Task<IReadOnlyList<Record>> ListRecordsAsync(string zoneId, string recordName, CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<Zone>($"zones/{zoneId}?recordName={recordName}&recordType=TXT", cancellationToken);

            return result?.Records ?? [];
        }

        public async Task CreateRecordAsync(string zoneId, string recordName, string content, CancellationToken cancellationToken = default)
        {
            var recordParam = new RecordParam
            {
                Name = recordName,
                Type = "TXT",
                Content = content,
                Ttl = 60
            };

            var response = await _httpClient.PostAsJsonAsync($"zones/{zoneId}/records", recordParam, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneId, string recordId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zoneId}/records/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("records")]
        public Record[]? Records { get; set; }
    }

    internal class RecordParam
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("prio")]
        public int Prio { get; set; } = 0;

        [JsonPropertyName("disabled")]
        public bool Disabled { get; set; } = false;
    }

    internal class Record : RecordParam
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }
    }
}
