using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class IonosCloudProvider(IonosCloudOptions options) : IDnsProvider
{
    private readonly IonosCloudDnsClient _ionosCloudDnsClient = new(options.Token);

    public string Name => "IONOS Cloud DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(120);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = new List<DnsZone>();

        await foreach (var zone in _ionosCloudDnsClient.ListZonesAsync(cancellationToken))
        {
            zones.Add(new DnsZone(this) { Id = zone.Id, Name = zone.Properties.ZoneName, NameServers = zone.Metadata.NameServers ?? [] });
        }

        return zones;
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            await _ionosCloudDnsClient.CreateRecordAsync(zone.Id, relativeRecordName, value, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var records = await _ionosCloudDnsClient.ListRecordsAsync(zone.Id, relativeRecordName, cancellationToken);

        foreach (var record in records)
        {
            try
            {
                await _ionosCloudDnsClient.DeleteRecordAsync(zone.Id, record.Id, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class IonosCloudDnsClient
    {
        public IonosCloudDnsClient(string token)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://dns.de-fra.ionos.com/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private readonly HttpClient _httpClient;

        public async IAsyncEnumerable<Zone> ListZonesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var offset = 0;

            while (true)
            {
                var result = await _httpClient.GetFromJsonAsync<Collection<Zone>>($"zones?filter.state=AVAILABLE&offset={offset}&limit=100", cancellationToken);

                if (result?.Items is null or { Length: 0 })
                {
                    break;
                }

                foreach (var zone in result.Items)
                {
                    yield return zone;
                }

                offset += result.Items.Length;
            }
        }

        public async Task<IReadOnlyList<Record>> ListRecordsAsync(string zoneId, string recordName, CancellationToken cancellationToken = default)
        {
            var result = await _httpClient.GetFromJsonAsync<Collection<Record>>($"records?filter.zoneId={zoneId}&filter.name={recordName}", cancellationToken);

            return result?.Items ?? [];
        }

        public async Task CreateRecordAsync(string zoneId, string recordName, string content, CancellationToken cancellationToken = default)
        {
            var recordParam = new RecordParam
            {
                Properties = new RecordProperties
                {
                    Name = recordName,
                    Type = "TXT",
                    Content = content,
                    Ttl = 60
                }
            };

            var response = await _httpClient.PostAsJsonAsync($"zones/{zoneId}/records", recordParam, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteRecordAsync(string zoneId, string recordId, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.DeleteAsync($"zones/{zoneId}/records/{recordId}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    internal class Collection<T>
    {
        [JsonPropertyName("items")]
        public required T[] Items { get; set; }
    }

    internal class Zone
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("metadata")]
        public required Metadata Metadata { get; set; }

        [JsonPropertyName("properties")]
        public required ZoneProperties Properties { get; set; }
    }

    internal class Metadata
    {
        [JsonPropertyName("state")]
        public required string State { get; set; }

        [JsonPropertyName("nameservers")]
        public string[]? NameServers { get; set; }
    }

    internal class ZoneProperties
    {
        [JsonPropertyName("zoneName")]
        public required string ZoneName { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

    internal class RecordParam
    {
        [JsonPropertyName("properties")]
        public required RecordProperties Properties { get; set; }
    }

    internal class Record : RecordParam
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }
    }

    internal class RecordProperties
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }
}
