using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class OvhProvider(OvhOptions options) : IDnsProvider
{
    private readonly OvhClient _ovhClient = new(options.Endpoint, options.ApplicationKey, options.ApplicationSecret, options.ConsumerKey);

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
                Ttl = 60
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
                // ignored
            }
        }

        await _ovhClient.RefreshZoneAsync(zone.Name, cancellationToken);
    }

    private class OvhClient
    {
        public OvhClient(string endpoint, string applicationKey, string applicationSecret, string consumerKey)
        {
            _httpClient = new HttpClient(new ApiKeyHandler(applicationKey, applicationSecret, consumerKey))
            {
                BaseAddress = new Uri(endpoint)
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
            using var request = CreateJsonRequest(HttpMethod.Post, $"domain/zone/{Uri.EscapeDataString(zoneName)}/record", record);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneName, long recordId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"domain/zone/{Uri.EscapeDataString(zoneName)}/record/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task RefreshZoneAsync(string zoneName, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"domain/zone/{Uri.EscapeDataString(zoneName)}/refresh", null, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private static HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string requestUri, T value)
        {
            return new HttpRequestMessage(method, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
            };
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
