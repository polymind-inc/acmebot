using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

using Acmebot.App.Extensions;
using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class DnsMadeEasyProvider(DnsMadeEasyOptions options) : IDnsProvider
{
    private readonly DnsMadeEasyClient _client = new(options.ApiKey, options.SecretKey);

    public string Name => "DNS Made Easy";

    public int PropagationSeconds => 30;

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync()
    {
        var zones = await _client.ListZonesAsync();

        return zones.Select(x => new DnsZone(this) { Id = x.Id, Name = x.Name }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            await _client.AddRecordAsync(zone.Id, new DnsEntry
            {
                Name = relativeRecordName,
                Type = "TXT",
                Expire = 60,
                Content = value
            });
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName)
    {
        var records = await _client.ListRecordsAsync(zone.Id);

        var recordsToDelete = records.Where(x => x.Name == relativeRecordName && x.Type == "TXT");

        foreach (var record in recordsToDelete)
        {
            await _client.DeleteRecordAsync(zone.Id, record);
        }
    }

    private class DnsMadeEasyClient
    {
        public DnsMadeEasyClient(string apiKey, string secretKey)
        {
            _httpClient = new HttpClient(new ApiKeyHandler(apiKey, secretKey, new HttpClientHandler()))
            {
                BaseAddress = new Uri("https://api.dnsmadeeasy.com/V2.0/dns/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Domain>> ListZonesAsync()
        {
            var response = await _httpClient.GetAsync("managed");

            response.EnsureSuccessStatusCode();

            var domains = await response.Content.ReadAsAsync<ListDomainsResult>();

            return domains.Domains;
        }

        public async Task<IReadOnlyList<DnsEntry>> ListRecordsAsync(string zoneId)
        {
            var response = await _httpClient.GetAsync($"managed/{zoneId}/records");

            response.EnsureSuccessStatusCode();

            var entries = await response.Content.ReadAsAsync<ListDnsEntriesResponse>();

            return entries.DnsEntries;
        }

        public async Task DeleteRecordAsync(string zoneId, DnsEntry entry)
        {
            var response = await _httpClient.DeleteAsync($"managed/{zoneId}/records/{entry.Id}");

            response.EnsureSuccessStatusCode();
        }

        public async Task AddRecordAsync(string zoneId, DnsEntry entry)
        {
            var response = await _httpClient.PostAsync($"managed/{zoneId}/records", entry);

            response.EnsureSuccessStatusCode();
        }

        private sealed class ApiKeyHandler : DelegatingHandler
        {
            private string ApiKey { get; }

            // ReSharper disable once InconsistentNaming
            private HMACSHA1 HMAC { get; }

            public ApiKeyHandler(string apiKey, string secretKey, HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
                ArgumentNullException.ThrowIfNull(apiKey);
                ArgumentNullException.ThrowIfNull(secretKey);
                ArgumentNullException.ThrowIfNull(innerHandler);

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new ArgumentException("API Key must be specified", nameof(apiKey));
                }

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    throw new ArgumentException("Secret Key must be specified", nameof(secretKey));
                }

                ApiKey = apiKey;

#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms - external specification
                HMAC = new HMACSHA1(Encoding.UTF8.GetBytes(secretKey));
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var currentTimeStr = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                var hmac = ComputeHash(currentTimeStr);

                request.Headers.Add("x-dnsme-apikey", ApiKey);
                request.Headers.Add("x-dnsme-requestdate", currentTimeStr);
                request.Headers.Add("x-dnsme-hmac", hmac);

                return base.SendAsync(request, cancellationToken);
            }

            private string ComputeHash(string s)
            {
                lock (HMAC)
                {
                    return BitConverter.ToString(HMAC.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "").ToLowerInvariant();
                }
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (disposing)
                {
                    HMAC.Dispose();
                }
            }
        }
    }

    private class ListDomainsResult
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<Domain>? Domains { get; set; }
    }

    private class Domain
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class ListDnsEntriesResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<DnsEntry>? DnsEntries { get; set; }
    }

    private class DnsEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("ttl")]
        public int Expire { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("value")]
        public string? Content { get; set; }
    }
}
