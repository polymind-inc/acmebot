using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class UnitedDomainsProvider(UnitedDomainsOptions options) : IDnsProvider
{
    private readonly UnitedDomainsClient _client = new(options.ApiKey);

    public string Name => "UnitedDomains";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _client.ListZonesAsync(cancellationToken);

        return zones.Select(z => new DnsZone(this) { Id = z.Id, Name = z.Name }).ToList();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var records = values.Select(v => new RecordParam
        {
            Name = recordName,
            Type = "TXT",
            Content = v,
            Ttl = 60
        }).ToArray();

        await _client.CreateRecordsAsync(zone.Id, records, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var zoneDetail = await _client.GetZoneAsync(zone.Id, recordName, "TXT", cancellationToken);

        foreach (var record in zoneDetail.Records ?? [])
        {
            try
            {
                await _client.DeleteRecordAsync(zone.Id, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class UnitedDomainsClient
    {
        public UnitedDomainsClient(string apiKey)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://dnsapi.united-domains.de/dns/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("acmebot");
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Zone>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<Zone[]>("v1/zones", cancellationToken);

            return result ?? [];
        }

        public async Task<CustomerZone> GetZoneAsync(string zoneId, string recordName, string recordType, CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<CustomerZone>(
                $"v1/zones/{zoneId}?recordName={Uri.EscapeDataString(recordName)}&recordType={Uri.EscapeDataString(recordType)}",
                cancellationToken);

            return result ?? new CustomerZone { Id = zoneId, Name = string.Empty };
        }

        public async Task CreateRecordsAsync(string zoneId, RecordParam[] records, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"v1/zones/{zoneId}/records", records, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneId, string recordId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"v1/zones/{zoneId}/records/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }

    internal class CustomerZone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("records")]
        public RecordResponse[]? Records { get; set; }
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
    }

    internal class RecordResponse
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }
}
