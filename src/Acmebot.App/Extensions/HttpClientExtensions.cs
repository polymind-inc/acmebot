using System.Text;
using System.Text.Json;

namespace Acmebot.App.Extensions;

internal static class HttpClientExtensions
{
    extension(HttpClient client)
    {
        public Task<HttpResponseMessage> PostAsync<T>(Uri requestUri, T value, CancellationToken cancellationToken = default) => client.PostAsync(requestUri, SerializeToJson(value), cancellationToken);
        public Task<HttpResponseMessage> PostAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default) => client.PostAsync(requestUri, SerializeToJson(value), cancellationToken);
        public Task<HttpResponseMessage> PutAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default) => client.PutAsync(requestUri, SerializeToJson(value), cancellationToken);
        public Task<HttpResponseMessage> PatchAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default) => client.PatchAsync(requestUri, SerializeToJson(value), cancellationToken);

        public Task<HttpResponseMessage> DeleteAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri)
            {
                Content = SerializeToJson(value)
            };

            return client.SendAsync(request, cancellationToken);
        }
    }

    private static StringContent SerializeToJson<T>(T value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
