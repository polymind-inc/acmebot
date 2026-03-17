using System.Net;
using System.Text.Json.Serialization;

using Acmebot.App.Extensions;
using Acmebot.App.Options;

using Akamai.EdgeGrid.Auth;

namespace Acmebot.App.Providers;

public class AkamaiEdgeDnsProvider : IDnsProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AkamaiEdgeDnsProvider(AkamaiEdgeDnsOptions options)
    {
        var credentials = new EdgeGridCredentials(
            host: options.Host,
            clientToken: options.ClientToken,
            clientSecret: options.ClientSecret,
            accessToken: options.AccessToken
        );

        _httpClient = EdgeGridSigner.CreateHttpClient(credentials);
        _baseUrl = $"https://{options.Host}/config-dns/v2/";
    }

    public string Name => "Akamai Edge DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(120);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await ListZonesInternalAsync(cancellationToken);

        var primaryZones = zones.Where(x => x.Type == "PRIMARY")
                    .Select(x => new DnsZone(this)
                    {
                        Id = x.Zone,
                        Name = x.Zone,
                        NameServers = []
                    })
                    .ToArray();

        return primaryZones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";
        var recordData = values.ToArray();

        var recordSet = new RecordSet
        {
            Name = recordName,
            Type = "TXT",
            Ttl = 60,
            Rdata = recordData
        };

        await CreateOrUpdateRecordAsync(zone.Name, recordName, "TXT", recordSet, cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var recordName = $"{relativeRecordName}.{zone.Name}";

        await DeleteRecordAsync(zone.Name, recordName, "TXT", cancellationToken);
    }

    private async Task<IReadOnlyList<ZoneResult>> ListZonesInternalAsync(CancellationToken cancellationToken = default)
    {
        var allZones = new List<ZoneResult>();
        var page = 1;
        const int pageSize = 100;

        while (true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}zones?page={page}&pageSize={pageSize}");
            var response = await _httpClient.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<ZonesResponse>();

            if (result?.Zones == null || result.Zones.Length == 0)
            {
                break;
            }

            allZones.AddRange(result.Zones);

            if (result.Zones.Length < pageSize || (result.Metadata != null && allZones.Count >= result.Metadata.TotalElements))
            {
                break;
            }

            page++;
        }

        return allZones;
    }

    private async Task<bool> RecordExistsAsync(string zone, string name, string type, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}zones/{zone}/names/{name}/types/{type}");
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        response.EnsureSuccessStatusCode();

        return false;
    }

    private async Task CreateOrUpdateRecordAsync(string zone, string name, string type, RecordSet recordSet, CancellationToken cancellationToken = default)
    {
        var recordExists = await RecordExistsAsync(zone, name, type, cancellationToken);
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(recordSet);

        var httpMethod = recordExists ? HttpMethod.Put : HttpMethod.Post;

        var request = new HttpRequestMessage(httpMethod, $"{_baseUrl}zones/{zone}/names/{name}/types/{type}")
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteRecordAsync(string zone, string name, string type, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}zones/{zone}/names/{name}/types/{type}");
        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private class ZonesResponse
    {
        [JsonPropertyName("metadata")]
        public Metadata? Metadata { get; set; }

        [JsonPropertyName("zones")]
        public ZoneResult[]? Zones { get; set; }
    }

    private class Metadata
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalElements")]
        public int TotalElements { get; set; }
    }

    private class ZoneResult
    {
        [JsonPropertyName("zone")]
        public required string Zone { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("contractId")]
        public string? ContractId { get; set; }

        [JsonPropertyName("activationState")]
        public string? ActivationState { get; set; }
    }

    private class RecordSet
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("rdata")]
        public string[]? Rdata { get; set; }
    }
}
