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

        return zones.Select(x => new DnsZone(this) { Id = x.Name, Name = x.Name }).ToArray();
    }

    public async Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        foreach (var value in values)
        {
            await _transIpClient.AddRecordAsync(zone.Name, new DnsEntry
            {
                Name = relativeRecordName,
                Type = "TXT",
                Expire = 60,
                Content = value
            }, cancellationToken);
        }
    }

    public async Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        var records = await _transIpClient.ListRecordsAsync(zone.Name, cancellationToken);

        var recordsToDelete = records.Where(r => r.Name == relativeRecordName && r.Type == "TXT");

        foreach (var record in recordsToDelete)
        {
            await _transIpClient.DeleteRecordAsync(zone.Name, record, cancellationToken);
        }
    }

    private class TransIpClient
    {
        public TransIpClient(string customerName, CryptographyClient cryptoClient)
        {
            _customerName = customerName;
            _cryptoClient = cryptoClient;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.transip.nl/v6/")
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private readonly HttpClient _httpClient;
        private readonly string _customerName;
        private readonly CryptographyClient _cryptoClient;

        private TransIpToken? _token;

        public async Task<IReadOnlyList<Domain>> ListDomainsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureLoggedInAsync(cancellationToken);

            var domains = await _httpClient.GetFromJsonAsync<ListDomainsResult>("domains", cancellationToken);

            return domains?.Domains ?? [];
        }

        public async Task<IReadOnlyList<DnsEntry>> ListRecordsAsync(string zoneName, CancellationToken cancellationToken = default)
        {
            await EnsureLoggedInAsync(cancellationToken);

            var entries = await _httpClient.GetFromJsonAsync<ListDnsEntriesResponse>($"domains/{zoneName}/dns", cancellationToken);

            return entries?.DnsEntries ?? [];
        }

        public async Task DeleteRecordAsync(string zoneName, DnsEntry entry, CancellationToken cancellationToken = default)
        {
            await EnsureLoggedInAsync(cancellationToken);

            var request = new DnsEntryRequest
            {
                DnsEntry = entry
            };

            var response = await _httpClient.DeleteAsync($"domains/{zoneName}/dns", request, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task AddRecordAsync(string zoneName, DnsEntry entry, CancellationToken cancellationToken = default)
        {
            await EnsureLoggedInAsync(cancellationToken);

            var request = new DnsEntryRequest
            {
                DnsEntry = entry
            };

            var response = await _httpClient.PostAsJsonAsync($"domains/{zoneName}/dns", request, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private async Task EnsureLoggedInAsync(CancellationToken cancellationToken = default)
        {
            if (_token?.IsValid() == true)
            {
                return;
            }

            if (_token is null)
            {
                _token = LoadToken();

                if (_token?.IsValid() == true && _customerName.Equals(_token.CustomerName))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token.Token);

                    var testResponse = await _httpClient.GetAsync("api-test", cancellationToken);

                    if (testResponse.IsSuccessStatusCode)
                    {
                        return;
                    }
                }

            }

            await CreateNewTokenAsync(cancellationToken);
        }

        private async Task CreateNewTokenAsync(CancellationToken cancellationToken = default)
        {
            var nonce = new byte[16];

            RandomNumberGenerator.Fill(nonce);

            var request = new TokenRequest
            {
                Login = _customerName,
                Nonce = Convert.ToBase64String(nonce)
            };

            var (signature, body) = await SignRequestAsync(request, cancellationToken);

            var response = await new HttpClient
            {
                BaseAddress = _httpClient.BaseAddress
            }.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, new Uri("auth"))
                {
                    Headers = { { "Signature", signature } },
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                }, cancellationToken);

            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadAsAsync<TokenResponse>();

            if (tokenResponse is null)
            {
                throw new Exception();
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.Token);

            _token = new TransIpToken
            {
                CustomerName = _customerName,
                Token = tokenResponse.Token,
                Expires = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.GetTokenExpiration())
            };

            StoreToken(_token);
        }

        private async Task<(string token, string body)> SignRequestAsync(object request, CancellationToken cancellationToken = default)
        {
            var body = JsonSerializer.Serialize(request);

            using var hasher = SHA512.Create();
            var bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(body));

            var signature = await _cryptoClient.SignAsync(SignatureAlgorithm.RS512, bytes, cancellationToken);

            return (Convert.ToBase64String(signature.Signature), body);
        }

        private void StoreToken(TransIpToken token)
        {
            var fullPath = Environment.ExpandEnvironmentVariables("%HOME%/.acmebot/transip_token.json");
            var directoryPath = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var json = JsonSerializer.Serialize(token);

            File.WriteAllText(fullPath, json);
        }

        private TransIpToken? LoadToken()
        {
            var fullPath = Environment.ExpandEnvironmentVariables("%HOME%/.acmebot/transip_token.json");

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var json = File.ReadAllText(fullPath);

            return JsonSerializer.Deserialize<TransIpToken>(json);
        }
    }

    private class TransIpToken
    {
        public string? CustomerName { get; init; }

        public string? Token { get; init; }

        public DateTimeOffset Expires { get; init; }

        public bool IsValid() => !string.IsNullOrEmpty(Token) && Expires - DateTimeOffset.Now > TimeSpan.FromMinutes(1);
    }

    private class TokenResponse
    {
        [JsonPropertyName("token")]
        public required string Token { get; set; }

        public long GetTokenExpiration()
        {
            var token = Token.Split('.')[1];

            var tokenBytes = System.Buffers.Text.Base64Url.DecodeFromChars(token);

            var tokenObject = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(tokenBytes));

            return tokenObject.GetProperty("exp").GetInt64();
        }
    }

    private class TokenRequest
    {
        [JsonPropertyName("login")]
        public string? Login { get; set; }

        [JsonPropertyName("nonce")]
        public string? Nonce { get; set; }

        [JsonPropertyName("read_only")]
        public bool ReadOnly { get; set; }

        [JsonPropertyName("expiration_time")]
        public string ExpirationTime { get; set; } = "4 weeks";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "Acmebot." + DateTime.UtcNow;

        [JsonPropertyName("global_key")]
        public bool GlobalKey { get; set; } = true;
    }

    private class ListDomainsResult
    {
        [JsonPropertyName("domains")]
        public IReadOnlyList<Domain>? Domains { get; set; }
    }

    private class Domain
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }

    private class ListDnsEntriesResponse
    {
        [JsonPropertyName("dnsEntries")]
        public IReadOnlyList<DnsEntry>? DnsEntries { get; set; }
    }

    private class DnsEntryRequest
    {
        [JsonPropertyName("dnsEntry")]
        public DnsEntry? DnsEntry { get; set; }
    }

    private class DnsEntry
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
