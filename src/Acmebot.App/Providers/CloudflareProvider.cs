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

        await foreach (var zone in _cloudflareClient.ListAllZonesAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Name, NameServers = zone.ActualNameServers });
        }

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        // 必要な検証用の値の数だけ新しく追加する
        foreach (var value in values)
        {
            await _cloudflareClient.CreateDnsRecordAsync(zone.Id, recordName, value, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var records = await _cloudflareClient.ListDnsRecordsAsync(zone.Id, recordName, cancellationToken);

        try
        {
            // 該当する全てのレコードを削除する
            foreach (var record in records)
            {
                await _cloudflareClient.DeleteDnsRecordAsync(zone.Id, record.Id, cancellationToken);
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignored
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

        public async IAsyncEnumerable<Zone> ListAllZonesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = 1;

            PagePaginationArray<Zone>? result;

            do
            {
                result = await _httpClient.GetFromJsonAsync<PagePaginationArray<Zone>>($"zones?page={page}&per_page=50&status=active", cancellationToken);

                if (result is null)
                {
                    break;
                }

                foreach (var zone in result.Result ?? [])
                {
                    yield return zone;
                }

            } while (page++ < (result.ResultInfo?.TotalPages ?? 1));
        }

        public async Task<IReadOnlyList<TxtRecord>> ListDnsRecordsAsync(string zone, string name, CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<PagePaginationArray<TxtRecord>>($"zones/{zone}/dns_records?type=TXT&name={name}&per_page=100", cancellationToken);

            return result?.Result ?? [];
        }

        public async Task CreateDnsRecordAsync(string zone, string name, string content, CancellationToken cancellationToken = default)
        {
            var recordParam = new TxtRecordParam
            {
                Name = name,
                Content = content,
                Ttl = 60
            };

            var response = await _httpClient.PostAsJsonAsync($"zones/{zone}/dns_records", recordParam, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteDnsRecordAsync(string zone, string id, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zone}/dns_records/{id}", cancellationToken);

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

    internal class TxtRecordParam
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; } = 1;

        [JsonPropertyName("type")]
        public string Type => "TXT";

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    internal class TxtRecord : TxtRecordParam
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }
    }
}
