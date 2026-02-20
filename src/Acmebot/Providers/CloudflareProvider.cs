using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Acmebot.Internal;
using Acmebot.Options;

namespace Acmebot.Providers;

public class CloudflareProvider(CloudflareOptions options) : IDnsProvider
{
    private readonly CloudflareDnsClient _cloudflareDnsClient = new(options.ApiToken);

    public string Name => "Cloudflare";

    public int PropagationSeconds => 10;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _cloudflareDnsClient.ListAllZonesAsync();

        // Zone API は Punycode されていない値を返すのでエンコードが必要
        return zones.Select(x => new DnsZone(this) { Id = x.Id, Name = x.Name, NameServers = x.ActualNameServers }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        // 必要な検証用の値の数だけ新しく追加する
        foreach (var value in values)
        {
            await _cloudflareDnsClient.CreateDnsRecordAsync(zone.Id, recordName, value);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var records = await _cloudflareDnsClient.GetDnsRecordsAsync(zone.Id, recordName);

        // 該当する全てのレコードを削除する
        foreach (var record in records)
        {
            await _cloudflareDnsClient.DeleteDnsRecordAsync(zone.Id, record.Id);
        }
    }

    private class CloudflareDnsClient
    {
        public CloudflareDnsClient(string apiToken)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.cloudflare.com/client/v4/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<ZoneResult>> ListAllZonesAsync()
        {
            var page = 1;
            var zones = new List<ZoneResult>();

            ApiResult<ZoneResult> result;

            do
            {
                result = await ListZonesAsync(page);

                zones.AddRange(result.Result);

            } while (page++ < result.ResultInfo.TotalPages);

            return zones;
        }

        public async Task<IReadOnlyList<DnsRecordResult>> GetDnsRecordsAsync(string zone, string name)
        {
            var response = await _httpClient.GetAsync($"zones/{zone}/dns_records?type=TXT&name={name}&per_page=100");

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<ApiResult<DnsRecordResult>>();

            return result.Result;
        }

        public async Task CreateDnsRecordAsync(string zone, string name, string content)
        {
            var response = await _httpClient.PostAsync($"zones/{zone}/dns_records", new { type = "TXT", name, content, ttl = 60 });

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteDnsRecordAsync(string zone, string id)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zone}/dns_records/{id}");

            response.EnsureSuccessStatusCode();
        }

        private async Task<ApiResult<ZoneResult>> ListZonesAsync(int page)
        {
            var response = await _httpClient.GetAsync($"zones?page={page}&per_page=50&status=active");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsAsync<ApiResult<ZoneResult>>();
        }
    }

    private class ApiResult<T>
    {
        [JsonPropertyName("result")]
        public T[] Result { get; set; }

        [JsonPropertyName("result_info")]
        public ResultInfo ResultInfo { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("errors")]
        public object[] Errors { get; set; }

        [JsonPropertyName("messages")]
        public object[] Messages { get; set; }
    }

    private class ResultInfo
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
    }

    private class ZoneResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("name_servers")]
        public string[] NameServers { get; set; }

        [JsonPropertyName("vanity_name_servers")]
        public string[] VanityNameServers { get; set; }

        [JsonIgnore]
        public string[] ActualNameServers => VanityNameServers is { Length: > 0 } ? VanityNameServers : NameServers;
    }

    private class DnsRecordResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        // ReSharper disable once InconsistentNaming
        [JsonPropertyName("ttl")]
        public int TTL { get; set; }
    }
}
