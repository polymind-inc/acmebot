using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class CloudflareProvider(CloudflareOptions options) : IDnsProvider
{
    private readonly CloudflareClient _cloudflareClient = new(options.ApiToken);

    public string Name => "Cloudflare";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var zone in _cloudflareClient.ListZonesAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Name, NameServers = zone.ActualNameServers });
        }

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        foreach (var value in values)
        {
            var record = new RecordParam
            {
                Name = recordName,
                Type = "TXT",
                Ttl = 60,
                Content = value
            };

            await _cloudflareClient.CreateDnsRecordAsync(zone.Id, record, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var records = await _cloudflareClient.ListDnsRecordsAsync(zone.Id, recordName, cancellationToken);

        foreach (var record in records)
        {
            try
            {
                await _cloudflareClient.DeleteDnsRecordAsync(zone.Id, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class CloudflareClient
    {
        public CloudflareClient(string apiToken)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.cloudflare.com/client/v4/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<Zone> ListZonesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = 1;

            PagePaginationArray<Zone>? result;

            do
            {
                result = await _httpClient.GetFromJsonAsync<PagePaginationArray<Zone>>($"zones?page={page}&per_page=50&status=active", cancellationToken);

                if (result?.Result is null or { Length: 0 })
                {
                    break;
                }

                foreach (var zone in result.Result)
                {
                    yield return zone;
                }

            } while (page++ < (result.ResultInfo?.TotalPages ?? 1));
        }

        public async Task<IReadOnlyList<Record>> ListDnsRecordsAsync(string zoneId, string recordName, CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<PagePaginationArray<Record>>($"zones/{zoneId}/dns_records?type=TXT&name={recordName}&per_page=100", cancellationToken);

            return result?.Result ?? [];
        }

        public async Task CreateDnsRecordAsync(string zoneId, RecordParam record, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"zones/{zoneId}/dns_records", record, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteDnsRecordAsync(string zoneId, string recordId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zoneId}/dns_records/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class PagePaginationArray<T>
    {
        [JsonPropertyName("result")]
        public T[]? Result { get; set; }

        [JsonPropertyName("result_info")]
        public ResultInfo? ResultInfo { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    internal class ResultInfo
    {
        [JsonPropertyName("total_pages")]
        public int? TotalPages { get; set; }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("name_servers")]
        public required string[] NameServers { get; set; }

        [JsonPropertyName("vanity_name_servers")]
        public string[]? VanityNameServers { get; set; }

        [JsonIgnore]
        public string[] ActualNameServers => VanityNameServers is { Length: > 0 } ? VanityNameServers : NameServers;
    }

    internal class RecordParam
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    internal class Record : RecordParam
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }
    }
}
