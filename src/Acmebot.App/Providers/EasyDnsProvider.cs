using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class EasyDnsProvider : IDnsProvider
{
    private readonly EasyDnsClient _easyDnsClient;

    public EasyDnsProvider(EasyDnsOptions options)
        : this(options, new HttpClientHandler())
    {
    }

    internal EasyDnsProvider(EasyDnsOptions options, HttpMessageHandler handler)
    {
        _easyDnsClient = new EasyDnsClient(options.ApiToken, options.ApiKey, handler);
    }

    public string Name => "EasyDNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var domains = await _easyDnsClient.ListDomainsAsync(cancellationToken);

        return domains.Select(x => new DnsZone { Id = x, Name = x }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            var record = new RecordParam
            {
                Domain = zone.Name,
                Host = relativeRecordName,
                Type = "TXT",
                Ttl = 60,
                RData = value
            };

            await _easyDnsClient.CreateRecordAsync(zone.Name, record, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var records = await _easyDnsClient.ListRecordsAsync(zone.Name, cancellationToken);

        var recordsToDelete = records.Where(x => string.Equals(x.Host, relativeRecordName, StringComparison.OrdinalIgnoreCase) && x.Type == "TXT");

        foreach (var record in recordsToDelete)
        {
            try
            {
                await _easyDnsClient.DeleteRecordAsync(zone.Name, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class EasyDnsClient
    {
        public EasyDnsClient(string apiToken, string apiKey, HttpMessageHandler handler)
        {
            _httpClient = DnsProviderHttpClient.Create("https://rest.easydns.net/", handler);

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiToken}:{apiKey}"));

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<string>> ListDomainsAsync(CancellationToken cancellationToken = default)
        {
            var userResponse = await _httpClient.GetFromJsonAsync<UserInfoResponse>("user", cancellationToken);
            var user = userResponse?.Data?.User;

            if (string.IsNullOrEmpty(user))
            {
                throw new InvalidOperationException("EasyDNS did not return the user associated with the API credentials.");
            }

            var domainResponse = await _httpClient.GetFromJsonAsync<DomainListResponse>($"domains/list/{Uri.EscapeDataString(user)}", cancellationToken);

            if (domainResponse?.Data is null)
            {
                return [];
            }

            // The domain list is returned as an object keyed by index, with an additional "user" entry.
            return domainResponse.Data
                .Where(x => x.Key != "user" && x.Value.ValueKind == JsonValueKind.Object)
                .Select(x => x.Value.TryGetProperty("name", out var name) ? name.GetString() : null)
                .OfType<string>()
                .ToArray();
        }

        public async Task<IReadOnlyList<Record>> ListRecordsAsync(string zone, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetFromJsonAsync<ZoneListResponse>($"zones/records/all/{zone}", cancellationToken);

            return response?.Data ?? [];
        }

        public async Task DeleteRecordAsync(string zone, long recordId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/records/{zone}/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task CreateRecordAsync(string zone, RecordParam record, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PutAsJsonAsync($"zones/records/add/{zone}/{record.Type}", record, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class UserInfoResponse
    {
        [JsonPropertyName("data")]
        public UserInfoData? Data { get; set; }
    }

    internal class UserInfoData
    {
        [JsonPropertyName("user")]
        public string? User { get; set; }
    }

    internal class DomainListResponse
    {
        [JsonPropertyName("data")]
        public Dictionary<string, JsonElement>? Data { get; set; }
    }

    internal class ZoneListResponse
    {
        [JsonPropertyName("data")]
        public Record[]? Data { get; set; }
    }

    internal class RecordParam
    {
        [JsonPropertyName("domain")]
        public required string Domain { get; set; }

        [JsonPropertyName("host")]
        public required string Host { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("rdata")]
        public required string RData { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("prio")]
        public int Prio { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    internal class Record : RecordParam
    {
        [JsonPropertyName("id")]
        public required long Id { get; set; }
    }
}
