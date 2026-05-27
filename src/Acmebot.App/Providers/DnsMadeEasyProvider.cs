using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class DnsMadeEasyProvider(DnsMadeEasyOptions options) : IDnsProvider
{
    private readonly DnsMadeEasyClient _dnsMadeEasyClient = new(options.ApiKey, options.SecretKey);

    public string Name => "DNS Made Easy";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _dnsMadeEasyClient.ListDomainsAsync(cancellationToken);

        return zones.Select(x => new DnsZone(this) { Id = x.Id.ToString(), Name = x.Name }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var zoneId = long.Parse(zone.Id);

        foreach (var value in values)
        {
            var record = new RecordParam
            {
                Name = relativeRecordName,
                Type = "TXT",
                Ttl = 60,
                Value = value
            };

            await _dnsMadeEasyClient.CreateRecordAsync(zoneId, record, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var zoneId = long.Parse(zone.Id);

        var records = await _dnsMadeEasyClient.ListRecordsAsync(zoneId, cancellationToken);

        var recordsToDelete = records.Where(x => x.Name == relativeRecordName && x.Type == "TXT");

        foreach (var record in recordsToDelete)
        {
            try
            {
                await _dnsMadeEasyClient.DeleteRecordAsync(zoneId, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class DnsMadeEasyClient
    {
        public DnsMadeEasyClient(string apiKey, string secretKey)
        {
            _httpClient = new HttpClient(new ApiKeyHandler(apiKey, secretKey))
            {
                BaseAddress = new Uri("https://api.dnsmadeeasy.com/V2.0/dns/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly HttpClient _httpClient;

        public Task<IReadOnlyList<Domain>> ListDomainsAsync(CancellationToken cancellationToken = default)
        {
            return GetAllPagesAsync<Domain>("managed", cancellationToken);
        }

        public Task<IReadOnlyList<Record>> ListRecordsAsync(long zoneId, CancellationToken cancellationToken = default)
        {
            return GetAllPagesAsync<Record>($"managed/{zoneId}/records", cancellationToken);
        }

        private async Task<IReadOnlyList<T>> GetAllPagesAsync<T>(string path, CancellationToken cancellationToken)
        {
            var items = new List<T>();
            var page = 0;

            while (true)
            {
                var result = await _httpClient.GetFromJsonAsync<PaginationArray<T>>($"{path}?page={page}", cancellationToken);

                if (result?.Data is { Length: > 0 })
                {
                    items.AddRange(result.Data);
                }

                if (result is null || page >= result.TotalPages - 1)
                {
                    break;
                }

                page++;
            }

            return items;
        }

        public async Task DeleteRecordAsync(long zoneId, long recordId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"managed/{zoneId}/records/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task CreateRecordAsync(long zoneId, RecordParam txtRecord, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"managed/{zoneId}/records", txtRecord, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private sealed class ApiKeyHandler(string apiKey, string secretKey) : DelegatingHandler(new HttpClientHandler())
        {
            private string ApiKey { get; } = apiKey;

            // ReSharper disable once InconsistentNaming
            private HMACSHA1 HMAC { get; } = new(Encoding.UTF8.GetBytes(secretKey));

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var requestDate = DateTimeOffset.UtcNow.ToString("r");
                var hmacHash = Convert.ToHexStringLower(HMAC.ComputeHash(Encoding.UTF8.GetBytes(requestDate)));

                request.Headers.Add("x-dnsme-apiKey", ApiKey);
                request.Headers.Add("x-dnsme-requestDate", requestDate);
                request.Headers.Add("x-dnsme-hmac", hmacHash);

                return base.SendAsync(request, cancellationToken);
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

    internal class PaginationArray<T>
    {
        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("data")]
        public T[]? Data { get; set; }
    }

    internal class Domain
    {
        [JsonPropertyName("id")]
        public required long Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }

    internal class RecordParam
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    internal class Record : RecordParam
    {
        [JsonPropertyName("id")]
        public required long Id { get; set; }
    }
}
