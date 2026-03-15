using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class CustomDnsProvider : IDnsProvider
{
    public CustomDnsProvider(CustomDnsOptions options)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(options.Endpoint)
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);

        PropagationDelay = TimeSpan.FromSeconds(options.PropagationSeconds);
    }

    private readonly HttpClient _httpClient;

    public string Name => "Custom DNS";

    public TimeSpan PropagationDelay { get; }

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _httpClient.GetFromJsonAsync<Zone[]>("zones", cancellationToken) ?? [];

        return zones.Select(x => new DnsZone(this) { Id = x.Id, Name = x.Name, NameServers = x.NameServers ?? [] }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var txtRecordParam = new TxtRecordParam
        {
            Ttl = 60,
            Values = values.ToArray()
        };

        var response = await _httpClient.PutAsJsonAsync($"zones/{zone.Id}/records/{recordName}", txtRecordParam, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        var response = await _httpClient.DeleteAsync($"zones/{zone.Id}/records/{recordName}", cancellationToken);

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
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
        public IReadOnlyList<string>? NameServers { get; set; }
    }

    internal class TxtRecordParam
    {
        [JsonPropertyName("type")]
        public string Type => "TXT";

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("values")]
        public IReadOnlyList<string>? Values { get; set; }
    }
}
