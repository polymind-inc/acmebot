using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class RegfishProvider(RegfishOptions options) : IDnsProvider
{
    private readonly RegfishClient _regfishClient = new(options.ApiKey);

    public string Name => "Regfish";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _regfishClient.ListZonesAsync(cancellationToken);

        return zones
            .Select(zone => new DnsZone(this)
            {
                Id = NormalizeName(zone.Domain),
                Name = NormalizeName(zone.Domain),
                NameServers = zone.DelegationNameServers?.Select(nameServer => NormalizeName(nameServer.Host)).ToArray() ?? []
            })
            .ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        var recordName = GetAbsoluteRecordName(zone.Name, relativeRecordName);

        foreach (var value in values)
        {
            var record = new RecordParam
            {
                Name = recordName,
                Type = "TXT",
                Data = value,
                Ttl = 60
            };

            await _regfishClient.CreateRecordAsync(record, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var absoluteRecordName = GetAbsoluteRecordName(zone.Name, relativeRecordName);
        var records = await _regfishClient.ListRecordsAsync(zone.Name, cancellationToken);

        foreach (var record in records)
        {
            if (!IsMatchingTxtRecord(record, zone.Name, absoluteRecordName))
            {
                continue;
            }

            await _regfishClient.DeleteRecordAsync(record.Id, cancellationToken);
        }
    }

    private static bool IsMatchingTxtRecord(DnsRecord record, string zoneName, string recordName)
    {
        if (!string.Equals(record.Type, "TXT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedRecordName = NormalizeName(recordName);
        var normalizedZoneName = NormalizeName(zoneName);
        var candidateName = NormalizeName(record.Name);

        if (string.Equals(candidateName, normalizedRecordName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(candidateName, "@", StringComparison.Ordinal) && string.Equals(normalizedRecordName, normalizedZoneName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeName(string value) => value.Trim().TrimEnd('.');

    private static string GetAbsoluteRecordName(string zoneName, string relativeRecordName)
    {
        var normalizedZoneName = NormalizeName(zoneName);

        if (string.IsNullOrWhiteSpace(relativeRecordName) || string.Equals(relativeRecordName.Trim(), "@", StringComparison.Ordinal))
        {
            return $"{normalizedZoneName}.";
        }

        var normalizedRelativeRecordName = NormalizeName(relativeRecordName);
        return $"{normalizedRelativeRecordName}.{normalizedZoneName}.";
    }
    private class RegfishClient
    {
        public RegfishClient(string apiKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.regfish.com/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Zone>> ListZonesAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("dns/zones", cancellationToken);
            var result = await ReadApiResponseAsync<Zone[]>(response, "list Regfish zones", cancellationToken: cancellationToken);

            return (result ?? [])
                .Where(zone => zone.Active)
                .ToArray();
        }

        public async Task<IReadOnlyList<DnsRecord>> ListRecordsAsync(string domain, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"dns/{Uri.EscapeDataString(domain)}/rr", cancellationToken);
            // Regfish can return HTTP 500 here for otherwise usable zones. Treat that as an empty
            // cleanup result so challenge creation can still proceed.
            var result = await ReadApiResponseAsync<DnsRecord[]>(
                response,
                $"list Regfish records for '{domain}'",
                HttpStatusCode.InternalServerError,
                cancellationToken: cancellationToken);

            return result ?? [];
        }

        public async Task CreateRecordAsync(RecordParam record, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync("dns/rr", record, cancellationToken);
            await ReadApiResponseAsync<DnsRecord>(response, $"create Regfish record '{record.Name}'", cancellationToken: cancellationToken);
        }

        public async Task DeleteRecordAsync(long recordId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"dns/rr/{recordId}", cancellationToken);
            await ReadApiResponseAsync<object>(response, $"delete Regfish record '{recordId}'", HttpStatusCode.NotFound, cancellationToken);
        }

        private static async Task<T?> ReadApiResponseAsync<T>(HttpResponseMessage response, string operation, HttpStatusCode? ignoredStatusCode = null, CancellationToken cancellationToken = default)
        {
            if (ignoredStatusCode == response.StatusCode)
            {
                return default;
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = result is null
                    ? $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})"
                    : JoinMessages(result.Message, result.Error);

                throw new HttpRequestException($"Regfish could not {operation}. {detail}".Trim(), null, response.StatusCode);
            }

            return EnsureSuccess(result, operation);
        }

        private static T EnsureSuccess<T>(ApiResponse<T>? response, string operation)
        {
            if (response is null)
            {
                throw new InvalidOperationException($"Regfish returned an empty response while attempting to {operation}.");
            }

            if (!response.Success)
            {
                throw new InvalidOperationException($"Regfish could not {operation}. {JoinMessages(response.Message, response.Error)}".Trim());
            }

            return response.Response;
        }

        private static string JoinMessages(params string?[] messages) => string.Join(" ", messages.Where(message => !string.IsNullOrWhiteSpace(message)));
    }

    internal class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("response")]
        public required T Response { get; set; }
    }

    internal class Zone
    {
        [JsonPropertyName("domain")]
        public required string Domain { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("delegation_nameservers")]
        public DelegationNameServer[]? DelegationNameServers { get; set; }
    }

    internal class DelegationNameServer
    {
        [JsonPropertyName("host")]
        public required string Host { get; set; }
    }

    internal class RecordParam
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("data")]
        public required string Data { get; set; }

        [JsonPropertyName("ttl")]
        public int Ttl { get; set; }
    }

    internal class DnsRecord
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }
    }
}
