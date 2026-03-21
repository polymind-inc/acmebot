using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Acmebot.App.Extensions;
using Acmebot.App.Options;

using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Acmebot.App.Providers;

public class TransIpProvider : IDnsProvider
{
    public TransIpProvider(AcmebotOptions acmeOptions, TransIpOptions options, TokenCredential credential)
    {
        var keyUri = new Uri(new Uri(acmeOptions.VaultBaseUrl), $"/keys/{options.PrivateKeyName}");
        var cryptoClient = new CryptographyClient(keyUri, credential);

        _transIpClient = new TransIpClient(options.CustomerName, cryptoClient);
    }

    private readonly TransIpClient _transIpClient;

    public string Name => "TransIP DNS";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(360);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _transIpClient.ListDomainsAsync(cancellationToken);

        return zones.Select(x => new DnsZone(this) { Id = x.Name, Name = x.Name, NameServers = x.NameServers?.Select(xs => xs.Hostname).ToArray() ?? [] }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, string[] values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            var entry = new DnsEntry
            {
                Name = relativeRecordName,
                Type = "TXT",
                Expire = 60,
                Content = value
            };

            await _transIpClient.CreateRecordAsync(zone.Name, entry, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var records = await _transIpClient.ListRecordsAsync(zone.Name, cancellationToken);

        var recordsToDelete = records.Where(x => x.Name == relativeRecordName && x.Type == "TXT");

        foreach (var record in recordsToDelete)
        {
            try
            {
                await _transIpClient.DeleteRecordAsync(zone.Name, record, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // ignored
            }
        }
    }

    private class TransIpClient
    {
        public TransIpClient(string customerName, CryptographyClient cryptoClient)
        {
            _httpClient = new HttpClient(new TransIpSignHandler(customerName, cryptoClient))
            {
                BaseAddress = new Uri("https://api.transip.nl/v6/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly HttpClient _httpClient;

        public async Task<IReadOnlyList<Domain>> ListDomainsAsync(CancellationToken cancellationToken = default)
        {
            var domains = await _httpClient.GetFromJsonAsync<ListDomainsResult>("domains?include=nameservers", cancellationToken);

            return domains?.Domains ?? [];
        }

        public async Task<IReadOnlyList<DnsEntry>> ListRecordsAsync(string zoneName, CancellationToken cancellationToken = default)
        {
            var entries = await _httpClient.GetFromJsonAsync<ListDnsEntriesResponse>($"domains/{zoneName}/dns", cancellationToken);

            return entries?.DnsEntries ?? [];
        }

        public async Task DeleteRecordAsync(string zoneName, DnsEntry entry, CancellationToken cancellationToken = default)
        {
            var request = new DnsEntryRequest
            {
                DnsEntry = entry
            };

            var response = await _httpClient.DeleteAsJsonAsync($"domains/{zoneName}/dns", request, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task CreateRecordAsync(string zoneName, DnsEntry entry, CancellationToken cancellationToken = default)
        {
            var request = new DnsEntryRequest
            {
                DnsEntry = entry
            };

            var response = await _httpClient.PostAsJsonAsync($"domains/{zoneName}/dns", request, cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }

    private class TransIpSignHandler(string customerName, CryptographyClient cryptoClient) : DelegatingHandler
    {
        private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("https://api.transip.nl/v6/") };

        private TransIpToken? _token;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await GetTokenAsync(cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            if (_token?.IsValid() ?? false)
            {
                return _token.Token;
            }

            _token = await AcquireTokenAsync(cancellationToken);

            return _token.Token;
        }

        private async Task<TransIpToken> AcquireTokenAsync(CancellationToken cancellationToken = default)
        {
            var nonce = new byte[16];

            RandomNumberGenerator.Fill(nonce);

            var tokenRequest = new TokenRequest
            {
                Login = customerName,
                Nonce = Convert.ToBase64String(nonce)
            };

            var body = JsonSerializer.Serialize(tokenRequest);

            var signature = await SignRequestAsync(body, cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, "auth")
            {
                Headers =
                {
                    { "Signature", signature }
                },
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (tokenResponse is null)
            {
                throw new Exception();
            }

            return new TransIpToken
            {
                Token = tokenResponse.Token,
                Expires = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.GetTokenExpiration())
            };
        }

        private async Task<string> SignRequestAsync(string body, CancellationToken cancellationToken = default)
        {
            var digest = SHA512.HashData(Encoding.UTF8.GetBytes(body));

            var signResult = await cryptoClient.SignAsync(SignatureAlgorithm.RS512, digest, cancellationToken);

            return Convert.ToBase64String(signResult.Signature);
        }
    }

    private class TransIpToken
    {
        public required string Token { get; init; }

        public DateTimeOffset Expires { get; init; }

        public bool IsValid() => !string.IsNullOrEmpty(Token) && Expires - DateTimeOffset.Now > TimeSpan.FromMinutes(1);
    }

    internal class TokenRequest
    {
        [JsonPropertyName("login")]
        public required string Login { get; set; }

        [JsonPropertyName("nonce")]
        public required string Nonce { get; set; }

        [JsonPropertyName("read_only")]
        public bool ReadOnly { get; set; } = false;

        [JsonPropertyName("expiration_time")]
        public string ExpirationTime { get; set; } = "4 weeks";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "Acmebot." + DateTime.UtcNow;

        [JsonPropertyName("global_key")]
        public bool GlobalKey { get; set; } = true;
    }

    internal class TokenResponse
    {
        [JsonPropertyName("token")]
        public required string Token { get; set; }

        public long GetTokenExpiration()
        {
            var token = Token.Split('.')[1];

            var tokenBytes = Base64Url.DecodeFromChars(token);

            var tokenObject = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(tokenBytes));

            return tokenObject.GetProperty("exp").GetInt64();
        }
    }

    internal class ListDomainsResult
    {
        [JsonPropertyName("domains")]
        public IReadOnlyList<Domain>? Domains { get; set; }
    }

    internal class Domain
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("nameservers")]
        public NameServer[]? NameServers { get; set; }
    }

    internal class NameServer
    {
        [JsonPropertyName("hostname")]
        public required string Hostname { get; set; }
    }

    internal class ListDnsEntriesResponse
    {
        [JsonPropertyName("dnsEntries")]
        public IReadOnlyList<DnsEntry>? DnsEntries { get; set; }
    }

    internal class DnsEntryRequest
    {
        [JsonPropertyName("dnsEntry")]
        public DnsEntry? DnsEntry { get; set; }
    }

    internal class DnsEntry
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("expire")]
        public int Expire { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
