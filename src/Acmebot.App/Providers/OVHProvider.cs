using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Acmebot.App.Options;

using Newtonsoft.Json;

namespace Acmebot.App.Providers;

public class OvhDnsProvider : IDnsProvider
{

    public OvhDnsProvider(OvhOptions options)
    {
        _client = new OvhClient(options.ApplicationKey, options.ApplicationSecret, options.ConsumerKey);
    }

    private readonly OvhClient _client;

    public string Name => "OVH";

    public TimeSpan PropagationDelay => TimeSpan.FromSeconds(300);

    public async Task<IReadOnlyList<DnsZone>> ListZonesAsync(CancellationToken cancellationToken = default)
    {
        var zones = await _client.ListZonesAsync();

        return zones.Select(x => new DnsZone(this) { Id = x, Name = x }).ToArray();
    }

    public Task CreateTxtRecordAsync(DnsZone zone, string relativeRecordName, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        var entries = values.Select(x => new DnsEntry { Name = relativeRecordName, Type = "TXT", TTL = 600, Data = x }).ToArray();
        return _client.AddRecordAsync(zone.Name, entries);
    }

    public Task DeleteTxtRecordAsync(DnsZone zone, string relativeRecordName, CancellationToken cancellationToken = default)
    {
        return _client.DeleteRecordAsync(zone.Name, "TXT", relativeRecordName);
    }

    private class OvhClient
    {

        public OvhClient(string applicationKey, string applicationSecret, string consumerKey)
        {
            _httpClient = new HttpClient();

            ArgumentNullException.ThrowIfNull(applicationKey);
            ArgumentNullException.ThrowIfNull(applicationSecret);
            ArgumentNullException.ThrowIfNull(consumerKey);

            _applicationKey = applicationKey;
            _applicationSecret = applicationSecret;
            _consumerKey = consumerKey;
        }

        private readonly HttpClient _httpClient;
        private readonly string _applicationKey;
        private readonly string _applicationSecret;
        private readonly string _consumerKey;

        public async Task<IReadOnlyList<string>> ListZonesAsync()
        {
            var url = "https://api.ovh.com/1.0/domain/zone";
            using (var requestMessage =
            new HttpRequestMessage(HttpMethod.Get, url))
            {
                var time = await GetTime();
                var signature = GenerateSignature(_applicationSecret, _consumerKey,
                    time, requestMessage.Method.Method, url);

                requestMessage.Headers.Add("X-Ovh-Application", _applicationKey);
                requestMessage.Headers.Add("X-Ovh-Consumer", _consumerKey);
                requestMessage.Headers.Add("X-Ovh-Signature", signature);
                requestMessage.Headers.Add("X-Ovh-Timestamp", time.ToString());


                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                var domains = await response.Content.ReadFromJsonAsync<string[]>();
                return domains!;
            }
        }


        public async Task DeleteRecordAsync(string zone, string type, string relativeRecordName)
        {

            var recordIds = Array.Empty<string>();
            var url = "https://api.ovh.com/1.0/domain/zone/" + zone + "/record";
            using (var requestMessage =
            new HttpRequestMessage(HttpMethod.Get, url))
            {
                var time = await GetTime();
                var signature = GenerateSignature(_applicationSecret, _consumerKey,
                    time, requestMessage.Method.Method, url);

                requestMessage.Headers.Add("X-Ovh-Application", _applicationKey);
                requestMessage.Headers.Add("X-Ovh-Consumer", _consumerKey);
                requestMessage.Headers.Add("X-Ovh-Signature", signature);
                requestMessage.Headers.Add("X-Ovh-Timestamp", time.ToString());


                var response = await _httpClient.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                recordIds = await response.Content.ReadFromJsonAsync<string[]>();
            }



            foreach (var recordId in recordIds!)
            {
                url = "https://api.ovh.com/1.0/domain/zone/" + zone + "/record/" + recordId;
                using (var requestMessage =
                new HttpRequestMessage(HttpMethod.Get, url))
                {
                    var time = await GetTime();
                    var signature = GenerateSignature(_applicationSecret, _consumerKey,
                        time, requestMessage.Method.Method, url);

                    requestMessage.Headers.Add("X-Ovh-Application", _applicationKey);
                    requestMessage.Headers.Add("X-Ovh-Consumer", _consumerKey);
                    requestMessage.Headers.Add("X-Ovh-Signature", signature);
                    requestMessage.Headers.Add("X-Ovh-Timestamp", time.ToString());

                    var response = await _httpClient.SendAsync(requestMessage);
                    response.EnsureSuccessStatusCode();
                    var ovhRecord = await response.Content.ReadFromJsonAsync<OvhRecord>();

                    if (ovhRecord!.fieldType == type && ovhRecord.subDomain == relativeRecordName)
                    {
                        url = "https://api.ovh.com/1.0/domain/zone/" + zone + "/record/" + recordId;
                        using (var requestMessage2 =
                        new HttpRequestMessage(HttpMethod.Delete, url))
                        {
                            var time2 = await GetTime();
                            var signature2 = GenerateSignature(_applicationSecret, _consumerKey,
                                time2, requestMessage2.Method.Method, url);

                            requestMessage2.Headers.Add("X-Ovh-Application", _applicationKey);
                            requestMessage2.Headers.Add("X-Ovh-Consumer", _consumerKey);
                            requestMessage2.Headers.Add("X-Ovh-Signature", signature2);
                            requestMessage2.Headers.Add("X-Ovh-Timestamp", time2.ToString());

                            var response2 = await _httpClient.SendAsync(requestMessage2);
                            response2.EnsureSuccessStatusCode();
                        }
                    }
                }
            }

        }
        public async Task AddRecordAsync(string zone, IReadOnlyList<DnsEntry> dnsEntries)
        {
            foreach (var dnsEntry in dnsEntries)
            {
                var recordUrl = "https://api.ovh.com/1.0/domain/zone/" + zone + "/record";
                using (var requestMessage =
                new HttpRequestMessage(HttpMethod.Post, recordUrl))
                {
                    var body = new
                    {
                        fieldType = dnsEntry.Type,
                        subDomain = dnsEntry.Name,
                        target = dnsEntry.Data,
                        ttl = dnsEntry.TTL
                    };
                    var bodyString = JsonConvert.SerializeObject(body);
                    requestMessage.Content = new StringContent(bodyString, Encoding.UTF8, "application/json");
                    var time = await GetTime();
                    var signature = GenerateSignature(_applicationSecret, _consumerKey,
                        time, requestMessage.Method.Method, recordUrl, bodyString);

                    requestMessage.Headers.Add("X-Ovh-Application", _applicationKey);
                    requestMessage.Headers.Add("X-Ovh-Consumer", _consumerKey);
                    requestMessage.Headers.Add("X-Ovh-Signature", signature);
                    requestMessage.Headers.Add("X-Ovh-Timestamp", time.ToString());


                    var response = await _httpClient.SendAsync(requestMessage);
                    response.EnsureSuccessStatusCode();
                }
            }
            var refreshUrl = "https://api.ovh.com/1.0/domain/zone/" + zone + "/refresh";
            using (var requestMessage =
            new HttpRequestMessage(HttpMethod.Post, refreshUrl))
            {
                var time = await GetTime();
                var signature = GenerateSignature(_applicationSecret, _consumerKey,
                    time, requestMessage.Method.Method, refreshUrl);

                requestMessage.Headers.Add("X-Ovh-Application", _applicationKey);
                requestMessage.Headers.Add("X-Ovh-Consumer", _consumerKey);
                requestMessage.Headers.Add("X-Ovh-Signature", signature);
                requestMessage.Headers.Add("X-Ovh-Timestamp", time.ToString());


                var responseRefresh = await _httpClient.SendAsync(requestMessage);
                responseRefresh.EnsureSuccessStatusCode();
            }

        }


        private static string GenerateSignature(string applicationSecret, string consumerKey,
            long currentTimestamp, string method, string target, string? data = null)
        {

            using (var sha1Hasher = SHA1.Create())
            {
                var toSign =
                    string.Join("+", applicationSecret, consumerKey, method,
                    target, data, currentTimestamp);
                var binaryHash = sha1Hasher.ComputeHash(Encoding.UTF8.GetBytes(toSign));
                var signature = string.Join("",
                    binaryHash.Select(x => x.ToString("X2"))).ToLower();
                return $"$1${signature}";
            }
        }

        private async Task<long> GetTime()
        {
            var response = await _httpClient.GetAsync("https://api.ovh.com/1.0/auth/time");

            response.EnsureSuccessStatusCode();
            var time = await response.Content.ReadFromJsonAsync<long>();
            return time;
        }
    }

    private class OvhRecord
    {
        public string? id { get; set; }
        public string? zone { get; set; }
        public string? subDomain { get; set; }
        public string? fieldType { get; set; }
        public string? target { get; set; }
        public int ttl { get; set; }
    }


    private class DnsEntry
    {
        public string? Data { get; set; }

        public string? Name { get; set; }

        public int TTL { get; set; }

        public string? Type { get; set; }
    }

}
