using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

using Acmebot.App.Extensions;
using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class GandiLiveDnsProvider(GandiLiveDnsOptions options) : IDnsProvider
{
    private readonly GandiLiveDnsClient _client = new(options.ApiKey);

    public string Name => "Gandi LiveDNS";

    public int PropagationSeconds => 300;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        return zones.Select(x => new DnsZone(this) { Id = x.Fqdn, Name = x.FqdnUnicode }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        return _client.AddRecordAsync(zone.Name, relativeRecordName, values);
    }

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        return _client.DeleteRecordAsync(zone.Name, relativeRecordName);
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

        public async Task<IReadOnlyList<Domain>> ListZonesAsync()
        {
            var response = await _httpClient.GetAsync("domain/domains");

            response.EnsureSuccessStatusCode();
            var domains = await response.Content.ReadAsAsync<Domain[]>();

            return domains.Where(x => x.Nameserver.Current == "livedns").ToArray();
        }

        public async Task DeleteRecordAsync(string zoneName, string relativeRecordName)
        {
            var response = await _httpClient.DeleteAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/TXT");

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task AddRecordAsync(string zoneName, string relativeRecordName, IEnumerable<string> values)
        {
            var response = await _httpClient.PostAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/TXT", new
            {
                rrset_values = values.ToArray(),
                rrset_ttl = 300 //300 is the minimal value
            });

            response.EnsureSuccessStatusCode();
        }
    }
    public class Domain
    {
        [JsonPropertyName("fqdn")]
        public string? Fqdn { get; set; }

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
        public string? FqdnUnicode { get; set; }
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
