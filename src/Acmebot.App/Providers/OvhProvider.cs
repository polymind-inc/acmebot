using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class OvhProvider(OvhOptions options) : IDnsProvider
{
    private const string OvhApiEndpoint = "https://eu.api.ovh.com/1.0/";
    private const int TxtRecordTtl = 60;

    private readonly OvhClient _ovhClient = new(options.ApplicationKey, options.ApplicationSecret, options.ConsumerKey);

    public string Name => "OVH";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _ovhClient.ListZonesAsync(cancellationToken);

        return zones.Select(x => new DnsZone(this) { Id = x, Name = x }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            var record = new RecordParam
            {
                FieldType = "TXT",
                SubDomain = relativeRecordName,
                Target = value,
                Ttl = TxtRecordTtl
            };

            await _ovhClient.CreateRecordAsync(zone.Name, record, cancellationToken);
        }

        await _ovhClient.RefreshZoneAsync(zone.Name, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordIds = await _ovhClient.ListTxtRecordIdsAsync(zone.Name, relativeRecordName, cancellationToken);

        foreach (var recordId in recordIds)
        {
            try
            {
                await _ovhClient.DeleteRecordAsync(zone.Name, recordId, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Record may have already been deleted by a concurrent cleanup or manual intervention.
                // ignored
            }
        }

        await _ovhClient.RefreshZoneAsync(zone.Name, cancellationToken);
    }

    private class OvhClient
    {
        public OvhClient(string applicationKey, string applicationSecret, string consumerKey)
        {
            // DNS providers in this project own their API clients; this one is constructed once by the singleton provider.
            _httpClient = new HttpClient(new ApiKeyHandler(applicationKey, applicationSecret, consumerKey))
            {
                BaseAddress = new Uri(OvhApiEndpoint)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<string>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            var zones = await _httpClient.GetFromJsonAsync<string[]>("domain/zone", cancellationToken);

            return zones ?? [];
        }

        public async Task<IReadOnlyList<long>> ListTxtRecordIdsAsync(string zoneName, string subDomain, CancellationToken cancellationToken = default)
        {
            var query = $"fieldType=TXT&subDomain={Uri.EscapeDataString(subDomain)}";
            var recordIds = await _httpClient.GetFromJsonAsync<long[]>($"domain/zone/{Uri.EscapeDataString(zoneName)}/record?{query}", cancellationToken);

            return recordIds ?? [];
        }

        public async Task CreateRecordAsync(string zoneName, RecordParam record, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync($"domain/zone/{Uri.EscapeDataString(zoneName)}/record", record, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneName, long recordId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"domain/zone/{Uri.EscapeDataString(zoneName)}/record/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task RefreshZoneAsync(string zoneName, CancellationToken cancellationToken = default)
        {
            // OVH requires refreshing the zone after record mutations before changes are published.
            using var response = await _httpClient.PostAsync($"domain/zone/{Uri.EscapeDataString(zoneName)}/refresh", null, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private sealed class ApiKeyHandler(string applicationKey, string applicationSecret, string consumerKey) : DelegatingHandler(new HttpClientHandler())
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                var signaturePayload = $"{applicationSecret}+{consumerKey}+{request.Method.Method}+{request.RequestUri}+{body}+{timestamp}";

                request.Headers.Add("X-Ovh-Application", applicationKey);
                request.Headers.Add("X-Ovh-Consumer", consumerKey);
                request.Headers.Add("X-Ovh-Timestamp", timestamp);
                // Known OVH API limitation: authentication signatures require SHA-1 and the "$1$" prefix.
                request.Headers.Add("X-Ovh-Signature", $"$1${Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(signaturePayload)))}");

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }

    internal class RecordParam
    {
        [JsonPropertyName("fieldType")]
        public required string FieldType { get; set; }

        [JsonPropertyName("subDomain")]
        public required string SubDomain { get; set; }

        [JsonPropertyName("target")]
        public required string Target { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }
    }
}
