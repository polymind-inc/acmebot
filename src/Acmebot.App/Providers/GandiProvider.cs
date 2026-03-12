using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Acmebot.App.Internal;
using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class GandiProvider(GandiOptions options) : IDnsProvider
{
    private readonly GandiClient _client = new(options.ApiKey);

    public string Name => "Gandi LiveDNS";

    public int PropagationSeconds => 300;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        // Do NOT include the PrimaryNameServer element from the DnsZone list for now,
        // the return value from Gandi when returning zones is not the expected value when doing the intersect at the Dns01Precondition method

        return zones.Select(x => new DnsZone(this) { Id = x.Uuid, Name = x.Name }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        return _client.AddRecordAsync(zone.Name, relativeRecordName, values);
    }

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        return _client.DeleteRecordAsync(zone.Name, relativeRecordName);
    }

    private class GandiClient
    {
        public GandiClient(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(apiKey);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://dns.api.gandi.net/api/v5/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Zone>> ListZonesAsync()
        {
            var response = await _httpClient.GetAsync("zones");

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsAsync<Zone[]>();
        }

        public async Task DeleteRecordAsync(string zoneName, string relativeRecordName)
        {
            var response = await _httpClient.DeleteAsync($"domains/{zoneName}/records/{relativeRecordName}/TXT");

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task AddRecordAsync(string zoneName, string relativeRecordName, IEnumerable<string> values)
        {
            var response = await _httpClient.PostAsync($"domains/{zoneName}/records/{relativeRecordName}/TXT", new
            {
                rrset_values = values.ToArray(),
                rrset_ttl = 300 //300 is the minimal value
            });

            response.EnsureSuccessStatusCode();
        }
    }

    public class Zone
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("primary_ns")]
        public string PrimaryNameServer { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("serial")]
        public int Serial { get; set; }

        [JsonPropertyName("user_uuid")]
        public string UserUuid { get; set; }

        [JsonPropertyName("refresh")]
        public int Refresh { get; set; }

        [JsonPropertyName("minimum")]
        public int Minimum { get; set; }

        [JsonPropertyName("expire")]
        public int Expire { get; set; }

        [JsonPropertyName("retry")]
        public int Retry { get; set; }
    }
}
