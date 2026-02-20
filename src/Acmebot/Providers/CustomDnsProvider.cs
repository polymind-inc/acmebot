using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Acmebot.Internal;
using Acmebot.Options;

namespace Acmebot.Providers;

public class CustomDnsProvider : IDnsProvider
{
    public CustomDnsProvider(CustomDnsOptions options)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.Endpoint)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation((string)options.ApiKeyHeaderName, (string?)options.ApiKey);

        PropagationSeconds = options.PropagationSeconds;
    }

    private readonly HttpClient _httpClient;

    public string Name => "Custom DNS";

    public int PropagationSeconds { get; }

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var response = await _httpClient.GetAsync("zones");

        response.EnsureSuccessStatusCode();

        var zones = await response.Content.ReadAsAsync<Zone[]>();

        return zones.Select(x => new DnsZone(this) { Id = x.Id, Name = x.Name, NameServers = x.NameServers ?? [] }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var response = await _httpClient.PutAsync($"zones/{zone.Id}/records/{recordName}", new { type = "TXT", ttl = 60, values });

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var response = await _httpClient.DeleteAsync($"zones/{zone.Id}/records/{recordName}");

        response.EnsureSuccessStatusCode();
    }

    private class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("nameServers")]
        public IReadOnlyList<string>? NameServers { get; set; }
    }
}
