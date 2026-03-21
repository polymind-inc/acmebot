using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class GoDaddyProvider(GoDaddyOptions options) : IDnsProvider
{
    private readonly GoDaddyClient _goDaddyClient = new(options.ApiKey, options.ApiSecret);

    public string Name => "GoDaddy";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(600);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var domain in _goDaddyClient.ListDomainsAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = domain.DomainId, Name = domain.Domain, NameServers = domain.NameServers ?? [] });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var entries = values.Select(x => new DnsEntry { Name = relativeRecordName, Type = "TXT", Ttl = 600, Data = x }).ToArray();

        return _goDaddyClient.CreateRecordAsync(zone.Name, entries, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _goDaddyClient.DeleteRecordAsync(zone.Name, "TXT", relativeRecordName, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignored
        }
    }

    private class GoDaddyClient
    {
        public GoDaddyClient(string apiKey, string apiSecret)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.godaddy.com/v1/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("sso-key", $"{apiKey}:{apiSecret}");
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<ZoneDomain> ListDomainsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var marker = "";

            while (true)
            {
                var domains = await _httpClient.GetFromJsonAsync<ZoneDomain[]>($"domains?statuses=ACTIVE&includes=nameServers&limit=1000&marker={marker}", cancellationToken);

                if (domains is null or { Length: 0 })
                {
                    break;
                }

                foreach (var domain in domains)
                {
                    yield return domain;
                }

                marker = domains[^1].Domain;
            }
        }

        public async Task DeleteRecordAsync(string domain, string type, string recordName, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"domains/{domain}/records/{type}/{recordName}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task CreateRecordAsync(string domain, IReadOnlyList<DnsEntry> entries, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PatchAsJsonAsync($"domains/{domain}/records", entries, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class ZoneDomain
    {
        [JsonPropertyName("domain")]
        public required string Domain { get; set; }

        [JsonPropertyName("domainId")]
        public required string DomainId { get; set; }

        [JsonPropertyName("nameServers")]
        public string[]? NameServers { get; set; }
    }

    internal class DnsEntry
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // ReSharper disable once InconsistentNaming
        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}
