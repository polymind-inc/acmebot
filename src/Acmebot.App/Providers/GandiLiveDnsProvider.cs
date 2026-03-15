using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class GandiLiveDnsProvider(GandiLiveDnsOptions options) : IDnsProvider
{
    private readonly GandiLiveDnsClient _client = new(options.ApiKey);

    public string Name => "Gandi LiveDNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(300);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _client.ListZonesAsync(cancellationToken);

        return zones.Select(x => new DnsZone(this) { Id = x.Fqdn, Name = x.FqdnUnicode }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
        => _client.AddRecordAsync(zone.Name, relativeRecordName, values, cancellationToken);

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteRecordAsync(zone.Name, relativeRecordName, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignored
        }
    }

    private class GandiLiveDnsClient
    {
        public GandiLiveDnsClient(string apiKey)
        {
            ArgumentNullException.ThrowIfNull(apiKey);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.gandi.net/v5/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Domain>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            var domains = await _httpClient.GetFromJsonAsync<Domain[]>("domain/domains", cancellationToken) ?? [];

            return domains.Where(x => x.Nameserver?.Current == "livedns").ToArray();
        }

        public async Task DeleteRecordAsync(string zoneName, string relativeRecordName, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/TXT", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task AddRecordAsync(string zoneName, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/TXT", new
            {
                rrset_values = values.ToArray(),
                rrset_ttl = 300 //300 is the minimal value
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    public class Domain
    {
        [JsonPropertyName("fqdn")]
        public required string Fqdn { get; set; }

        [JsonPropertyName("tld")]
        public string? Tld { get; set; }

        [JsonPropertyName("status")]
        public List<string>? Status { get; set; }

        [JsonPropertyName("dates")]
        public Dates? Dates { get; set; }

        [JsonPropertyName("nameserver")]
        public Nameserver? Nameserver { get; set; }

        [JsonPropertyName("autorenew")]
        public bool Autorenew { get; set; }

        [JsonPropertyName("domain_owner")]
        public string? DomainOwner { get; set; }

        [JsonPropertyName("orga_owner")]
        public string? OrgaOwner { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("fqdn_unicode")]
        public required string FqdnUnicode { get; set; }
    }

    public class Dates
    {
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("registry_created_at")]
        public DateTime RegistryCreatedAt { get; set; }

        [JsonPropertyName("registry_ends_at")]
        public DateTime RegistryEndsAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class Nameserver
    {
        [JsonPropertyName("current")]
        public string? Current { get; set; }
    }
}
