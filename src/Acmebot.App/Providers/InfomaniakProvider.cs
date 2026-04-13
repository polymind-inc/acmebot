using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

/// <summary>
/// DNS provider for Infomaniak using the REST API v1.
/// Docs: https://developer.infomaniak.com/
/// Requires an API token with the "domain" scope.
/// </summary>
public class InfomaniakProvider : IDnsProvider
{
    public InfomaniakProvider(InfomaniakOptions options)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.infomaniak.com/1/") };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
        _client = new InfomaniakClient(http);
    }

    /// <summary>Internal constructor for unit tests — inject a pre-configured HttpClient.</summary>
    internal InfomaniakProvider(HttpClient httpClient)
    {
        _client = new InfomaniakClient(httpClient);
    }

    private readonly InfomaniakClient _client;

    public string Name => "Infomaniak";

    /// <summary>Infomaniak DNS propagation is typically fast.</summary>
    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(30);

    /// <summary>Returns all DNS zones available for the configured token.</summary>
    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _client.ListZonesAsync(cancellationToken);

        return zones
            .Select(z => new DnsZone(this) { Id = z.Id.ToString(), Name = z.CustomerName })
            .ToList();
    }

    /// <summary>Creates one TXT record per value for the ACME DNS-01 challenge.</summary>
    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            await _client.CreateRecordAsync(zone.Id, relativeRecordName, value, cancellationToken);
        }
    }

    /// <summary>Deletes all TXT records matching the challenge record name.</summary>
    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var records = await _client.ListRecordsAsync(zone.Id, relativeRecordName, cancellationToken);

        foreach (var record in records)
        {
            try
            {
                await _client.DeleteRecordAsync(zone.Id, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Record already deleted — safe to ignore
            }
        }
    }

    /// <summary>HTTP client wrapper for the Infomaniak DNS API.</summary>
    private class InfomaniakClient(HttpClient http)
    {
        private readonly HttpClient _http = http;

        /// <summary>GET /1/zone — returns all DNS zones accessible with the token.</summary>
        public async Task<IReadOnlyList<Zone>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Zone[]>>("zone", cancellationToken);
            return response?.Data ?? [];
        }

        /// <summary>GET /1/zone/{zoneId}/record — returns TXT records matching the given source.</summary>
        public async Task<IReadOnlyList<Record>> ListRecordsAsync(string zoneId, string source, CancellationToken cancellationToken = default)
        {
            var response = await _http.GetFromJsonAsync<ApiResponse<Record[]>>($"zone/{zoneId}/record?type=TXT&source={source}", cancellationToken);
            return response?.Data ?? [];
        }

        /// <summary>POST /1/zone/{zoneId}/record — creates a TXT record for the ACME challenge.</summary>
        public async Task CreateRecordAsync(string zoneId, string source, string target, CancellationToken cancellationToken = default)
        {
            var body = new { type = "TXT", source, target, ttl = 60 };
            var response = await _http.PostAsJsonAsync($"zone/{zoneId}/record", body, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>DELETE /1/zone/{zoneId}/record/{recordId} — removes a specific DNS record.</summary>
        public async Task DeleteRecordAsync(string zoneId, string recordId, CancellationToken cancellationToken = default)
        {
            var response = await _http.DeleteAsync($"zone/{zoneId}/record/{recordId}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    internal class ApiResponse<T>
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required int Id { get; set; }

        /// <summary>Domain name as returned by Infomaniak (e.g. "example.com").</summary>
        [JsonPropertyName("customer_name")]
        public required string CustomerName { get; set; }
    }

    internal class Record
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("source")]
        public required string Source { get; set; }

        [JsonPropertyName("target")]
        public required string Target { get; set; }
    }
}
