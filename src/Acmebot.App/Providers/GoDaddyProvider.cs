using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Acmebot.App.Internal;
using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class GoDaddyProvider(GoDaddyOptions options) : IDnsProvider
{
    private readonly GoDaddyClient _client = new(options.ApiKey, options.ApiSecret);

    public string Name => "GoDaddy";

    public int PropagationSeconds => 600;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        return zones.Select(x => new DnsZone(this) { Id = x.DomainId, Name = x.Domain, NameServers = x.NameServers }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var entries = values.Select(x => new DnsEntry { Name = relativeRecordName, Type = "TXT", TTL = 600, Data = x }).ToArray();

        return _client.AddRecordAsync(zone.Name, entries);
    }

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        return _client.DeleteRecordAsync(zone.Name, "TXT", relativeRecordName);
    }

    private class GoDaddyClient
    {
        public GoDaddyClient(string apiKey, string apiSecret)
        {
            ArgumentNullException.ThrowIfNull(apiKey);
            ArgumentNullException.ThrowIfNull(apiSecret);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.godaddy.com/v1/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<ZoneDomain>> ListZonesAsync()
        {
            const int limit = 100;

            var marker = "";
            var allActiveDomains = new List<ZoneDomain>();

            while (true)
            {
                var response = await _httpClient.GetAsync($"domains?statuses=ACTIVE&includes=nameServers&limit={limit}{marker}");

                response.EnsureSuccessStatusCode();

                var domains = await response.Content.ReadAsAsync<ZoneDomain[]>();

                if (domains.Length == 0)
                {
                    break;
                }

                allActiveDomains.AddRange(domains);

                marker = $"&marker={domains[^1].Domain}";
            }

            return allActiveDomains;
        }

        public async Task DeleteRecordAsync(string domain, string type, string name)
        {
            var response = await _httpClient.DeleteAsync($"domains/{domain}/records/{type}/{name}");

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task AddRecordAsync(string domain, IReadOnlyList<DnsEntry> entries)
        {
            var response = await _httpClient.PatchAsync($"domains/{domain}/records", entries);

            response.EnsureSuccessStatusCode();
        }
    }

    private class ZoneDomain
    {
        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("domainId")]
        public string? DomainId { get; set; }

        [JsonPropertyName("nameServers")]
        public string[]? NameServers { get; set; }
    }

    private class DnsEntry
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // ReSharper disable once InconsistentNaming
        [JsonPropertyName("ttl")]
        public int TTL { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}
