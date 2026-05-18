using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class PowerDnsProvider(PowerDnsOptions options) : IDnsProvider
{
    private readonly PowerDnsClient _powerDnsClient = new(options.Endpoint, options.ApiKey, options.ServerId);

    public string Name => "PowerDNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _powerDnsClient.ListZonesAsync(cancellationToken);

        return zones
            .Select(zone => new DnsZone(this) { Id = zone.Id, Name = StripTrailingDot(zone.Name), NameServers = [] })
            .ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var rrset = new RrSet
        {
            Name = BuildRecordName(zone.Name, relativeRecordName),
            Type = "TXT",
            Ttl = 60,
            ChangeType = "REPLACE",
            Records = values.Select(value => new Record { Content = QuoteTxtValue(value), Disabled = false }).ToArray()
        };

        return _powerDnsClient.PatchZoneAsync(zone.Id, [rrset], cancellationToken);
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var rrset = new RrSet
        {
            Name = BuildRecordName(zone.Name, relativeRecordName),
            Type = "TXT",
            ChangeType = "DELETE"
        };

        try
        {
            await _powerDnsClient.PatchZoneAsync(zone.Id, [rrset], cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ignored
        }
    }

    private static string StripTrailingDot(string value) => value.EndsWith('.') ? value[..^1] : value;

    private static string QuoteTxtValue(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string BuildRecordName(string zoneName, string relativeRecordName)
    {
        var normalizedZone = StripTrailingDot(zoneName);

        if (string.IsNullOrWhiteSpace(relativeRecordName) || relativeRecordName == "@")
        {
            return $"{normalizedZone}.";
        }

        return $"{StripTrailingDot(relativeRecordName)}.{normalizedZone}.";
    }

    private class PowerDnsClient
    {
        public PowerDnsClient(Uri endpoint, string apiKey, string serverId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

            var baseAddress = endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");

            _httpClient = new HttpClient
            {
                BaseAddress = baseAddress
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

            _serverId = Uri.EscapeDataString(serverId);
        }

        private readonly HttpClient _httpClient;
        private readonly string _serverId;

        public async Task<IReadOnlyList<Zone>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            var zones = await _httpClient.GetFromJsonAsync<Zone[]>($"servers/{_serverId}/zones", cancellationToken);

            return zones ?? [];
        }

        public async Task PatchZoneAsync(string zoneId, IReadOnlyList<RrSet> rrsets, CancellationToken cancellationToken = default)
        {
            var request = new PatchZoneRequest { RrSets = rrsets };

            var response = await _httpClient.PatchAsJsonAsync($"servers/{_serverId}/zones/{Uri.EscapeDataString(zoneId)}", request, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }
    }

    internal class PatchZoneRequest
    {
        [JsonPropertyName("rrsets")]
        public required IReadOnlyList<RrSet> RrSets { get; set; }
    }

    internal class RrSet
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("ttl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Ttl { get; set; }

        [JsonPropertyName("changetype")]
        public required string ChangeType { get; set; }

        [JsonPropertyName("records")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<Record>? Records { get; set; }
    }

    internal class Record
    {
        [JsonPropertyName("content")]
        public required string Content { get; set; }

        [JsonPropertyName("disabled")]
        public bool Disabled { get; set; }
    }
}
