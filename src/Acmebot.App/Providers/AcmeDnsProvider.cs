using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Options;

namespace Acmebot.App.Providers;

public class AcmeDnsProvider : IDnsProvider
{
    private readonly AcmeDnsClient _acmeDnsClient;
    private readonly IReadOnlyDictionary<string, AcmeDnsZoneOptions> _zoneOptions;

    public AcmeDnsProvider(AcmeDnsOptions options)
    {
        _acmeDnsClient = new AcmeDnsClient(options.Endpoint);

        var duplicateZoneNames = options.Zones.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                                              .Where(x => x.Count() > 1)
                                              .Select(x => x.Key)
                                              .ToArray();

        if (duplicateZoneNames.Length > 0)
        {
            throw new InvalidOperationException($"AcmeDns zone names must be unique. Duplicates: {string.Join(", ", duplicateZoneNames)}.");
        }

        _zoneOptions = options.Zones.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        PropagationDelay = TimeSpan.FromSeconds(options.PropagationSeconds);
    }

    public string Name => "acme-dns";

    public TimeSpan PropagationDelay { get; }

    public Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DnsZone> zones = _zoneOptions.Values
                                                    .Select(x => new DnsZone(this)
                                                    {
                                                        Id = x.Subdomain,
                                                        Name = x.Name
                                                    })
                                                    .ToArray();

        return Task.FromResult(zones);
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        if (!_zoneOptions.TryGetValue(zone.Name, out var zoneOption))
        {
            throw new InvalidOperationException($"No acme-dns configuration was found for zone '{zone.Name}'.");
        }

        foreach (var value in values.Distinct(StringComparer.Ordinal))
        {
            await _acmeDnsClient.UpdateTxtRecordAsync(zoneOption, value, cancellationToken);
        }
    }

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        // acme-dns exposes update semantics for the delegated record but no delete operation.
        return Task.CompletedTask;
    }

    private class AcmeDnsClient
    {
        public AcmeDnsClient(string endpoint)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(AppendTrailingSlash(endpoint))
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly HttpClient _httpClient;

        public async Task UpdateTxtRecordAsync(AcmeDnsZoneOptions zone, string txtValue, CancellationToken cancellationToken = default)
        {
            var request = new UpdateRequest
            {
                Subdomain = zone.Subdomain,
                Txt = txtValue
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, "update")
            {
                Content = JsonContent.Create(request)
            };

            message.Headers.TryAddWithoutValidation("X-Api-User", zone.Username);
            message.Headers.TryAddWithoutValidation("X-Api-Key", zone.Password);

            var response = await _httpClient.SendAsync(message, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private static string AppendTrailingSlash(string endpoint)
        {
            return endpoint.EndsWith("/", StringComparison.Ordinal) ? endpoint : $"{endpoint}/";
        }
    }

    private class UpdateRequest
    {
        [JsonPropertyName("subdomain")]
        public required string Subdomain { get; set; }

        [JsonPropertyName("txt")]
        public required string Txt { get; set; }
    }
}
