using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class GandiLiveDnsProvider(GandiLiveDnsOptions options) : IDnsProvider
{
    private readonly GandiLiveDnsClient _gandiLiveDnsClient = new(options.ApiKey);

    public string Name => "Gandi LiveDNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(300);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var domain in _gandiLiveDnsClient.ListDomainsAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = domain.Fqdn, Name = domain.Fqdn });
        }

        return zones;
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var record = new Record
        {
            RrsetType = "TXT",
            RrsetValues = values,
            RrsetTtl = 300
        };

        return _gandiLiveDnsClient.CreateRecordAsync(zone.Name, relativeRecordName, record, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _gandiLiveDnsClient.DeleteRecordAsync(zone.Name, relativeRecordName, "TXT", cancellationToken);
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
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.gandi.net/v5/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<Domain> ListDomainsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = 1;

            while (true)
            {
                var domains = await _httpClient.GetFromJsonAsync<Domain[]>($"livedns/domains?page={page}", cancellationToken);

                if (domains is null or { Length: 0 })
                {
                    break;
                }

                foreach (var domain in domains)
                {
                    yield return domain;
                }

                page++;
            }
        }

        public async Task DeleteRecordAsync(string zoneName, string relativeRecordName, string rrsetType, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}/{rrsetType}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task CreateRecordAsync(string zoneName, string relativeRecordName, Record record, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"livedns/domains/{zoneName}/records/{relativeRecordName}", record, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class Domain
    {
        [JsonPropertyName("fqdn")]
        public required string Fqdn { get; set; }
    }

    internal class Record
    {
        [JsonPropertyName("rrset_type")]
        public required string RrsetType { get; set; }

        [JsonPropertyName("rrset_values")]
        public required string[] RrsetValues { get; set; }

        [JsonPropertyName("rrset_ttl")]
        public int? RrsetTtl { get; set; } //300 is the minimal value
    }
}
